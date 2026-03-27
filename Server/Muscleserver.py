import socket
import struct
import numpy as np
import cupy as cp
import cupyx
import scipy.sparse as sp
import trimesh
from scipy.spatial import cKDTree
import time
import threading
import csv
import warnings



class ContinuumGNN:
    """
    Physically-based simulator implementing the Cauchy Momentum Equation (Eq. 2)
    using Finite Strain Theory.

    Unlike simple mass-spring systems, this class computes forces based on the 
    Deformation Gradient Tensor (F), allowing for accurate handling of material 
    rotation and non-linear geometric deformations (Cauchy-Elasticity).

    Governing Equations:
    1. Kinematics: x = X + u (Current configuration)
    2. Deformation Gradient: F = grad(x) w.r.t Reference X (Eq. 6)
    3. Constitutive Law: P = P(F) (Cauchy Elastic Material) (Eq. 5)
    4. Equilibrium: Div(P) + f_ext = rho * a (Strong form, Eq. 2)
    """
    def __init__(self, nodes, edges, fixed_mask, E, nu, density, dt):
        """
        Initializes the continuum mechanics solver and precomputes shape matrices
        for meshless gradient estimation.

        Args:
            nodes (np.array): (N, 3) Matrix of node positions in the UNDEFORMED 
                              Reference Configuration (Material Coordinates X).
            edges (np.array): (M, 2) List of connected node pairs (graph topology).
            fixed_mask (np.array): Boolean array indicating nodes with Dirichlet 
                                   boundary conditions (u=0).
            E (float): Young's Modulus (measure of stiffness).
            nu (float): Poisson's Ratio (measure of compressibility, ~0.45-0.49 for muscle).
            density (float): Material density (rho).
            dt (float): Time step for numerical integration.

    """
    
        self.X = cp.asarray(nodes, dtype=cp.float32) # Material Coordinates X (Static Reference)
        self.num_nodes = self.X.shape[0]
        self.fixed_mask = cp.asarray(fixed_mask)
        self.dt = dt
        self.density = density
        
        #Constitutive Parameters (Lamé Constants)
        # Converted from Young's Modulus (E) and Poisson's Ratio (nu).
        # These define the linear elastic response within the St. Venant-Kirchhoff model.
        self.mu = E / (2 * (1 + nu))           # Second Lamé parameter (Shear Modulus)
        self.lam = (E * nu) / ((1 + nu) * (1 - 2 * nu)) # First Lamé parameter (Bulk Modulus)
        
        # Precomputing Reference Gradients (Meshless Shape Functions)
        # To calculate the Deformation Gradient F = dx/dX, we need to approximate spatial 
        # derivatives on a point cloud.  We precompute the inverse of the "Moment Matrix" (Covariance of positions)  in the Reference Configuration.
        
        
        print("Precomputing Reference Gradients (Inverse Shape Matrices)...")
        
        #  Graph Topology Setup (neighbour lookup)
        
        edges_cpu = cp.asnumpy(edges)
        bidirectional = np.vstack([edges_cpu, edges_cpu[:, ::-1]])
        self.all_edges = cp.asarray(bidirectional, dtype=cp.int32)
        
        self.row = self.all_edges[:, 0] #src index (nodes src)
        self.col = self.all_edges[:, 1] # destinations index (nodes destination)       
        
        # Storage for Inverse Moment Matrices B_inv (3x3 for each node)
        # Mathematical basis: F_i * Sum((X_j - X_i)*(X_j - X_i)^T) = Sum((x_j - x_i)*(X_j - X_i)^T)
        
        # compute dx for each node given its neighbour
        ## dX: Vector from node i to neighbors j in Reference configuration
        dX = self.X[self.col] - self.X[self.row]
        dX = dX[..., None] # add per matmul (N_edges, 3, 1)
        
        # Compute Moment Matrix M = Sum (dX * dX^T)
        # This represents the geometric distribution of neighbors.
        M_edge = cp.matmul(dX, dX.transpose(0, 2, 1))
        
        # M_nodes shape: (N_nodes, 3, 3)
        self.M_nodes = cp.zeros((self.num_nodes, 3, 3), dtype=cp.float32)
        
        # we compute M matrix of momentum using self.row like indeces and M_edge like values
        cupyx.scatter_add(self.M_nodes, self.row, M_edge)
        
        # Regularization: Add epsilon to diagonal to prevent singular matrix inversion
        I = cp.eye(3, dtype=cp.float32)
        self.M_nodes += I[None, :, :] * 1e-9
        
        
        self.inv_moments = cp.linalg.inv(self.M_nodes)
        
        # preallocated identity matrix for next computations
        self.I_batch = cp.repeat(I[None, :, :], self.num_nodes, axis=0)
        
    def compute_forces(self, u):
        """
        Computes the internal elastic forces acting on nodes based on the 
        Cauchy Momentum Equation.

        Pipeline:
        1. Kinematics: Compute Deformation Gradient F.
        2. Strain: Compute Green-Lagrange Strain E (removes rigid rotation).
        3. Stress: Compute Second Piola-Kirchhoff Stress S (Constitutive Law).
        4. Spatial Stress: Map S -> First Piola-Kirchhoff Stress P.
        5. Forces: Compute divergence of P (Div P).

        Args:
            u (np.array): Current displacement field (N, 3).
        
        Returns:
            forces (np.array): Internal force vectors for each node (N, 3).
        """
        # Current Spatial Configuration: x = X + u (Eq. 1)
        current_x = self.X + u
        
        
        
        #Compute Deformation Gradient F
        # F maps vectors from Reference (X) to Current (x) configuration: dx = F * dX.
        # F captures stretch, shear, and rotation.
       
        
        dx_edges = current_x[self.col] - current_x[self.row] # (N_edges, 3)
        dX_edges = self.X[self.col] - self.X[self.row]       # (N_edges, 3)
        
        # Reshape for matrix multiplication (N, 3, 1)
        dx_edges = dx_edges[..., None]
        dX_edges = dX_edges[..., None]
        
    
        
        # Numerator contribution: dx * dX^T -> (N_edges, 3, 3)
        num_contrib = cp.matmul(dx_edges, dX_edges.transpose(0, 2, 1))
        
        
        numerator = cp.zeros((self.num_nodes, 3, 3), dtype=cp.float32)
        cupyx.scatter_add(numerator, self.row, num_contrib)
        
        
        #F = Numerator * M_inv -> (N_nodes, 3, 3)
        Fs = cp.matmul(numerator, self.inv_moments)
        
        #Compute Stress Tensor P (Constitutive Law) ---
        # We implement a Saint Venant-Kirchhoff material. 
        # This is a "Cauchy Elastic" model extended to large deformations.
        
        # Green-Lagrange Strain Tensor E:
        # E = 0.5 * (F^T * F - I)
        # Crucial: E is invariant to rigid body rotations, solving the issue
                
        FT = Fs.transpose(0, 2, 1)
        FTF = cp.matmul(FT, Fs)
        E_strain = 0.5 * (FTF - self.I_batch)
        
        # Trace of E (sum of diagonals) -> (N, 1, 1) per broadcasting
        trE = cp.trace(E_strain, axis1=1, axis2=2)[:, None, None]
        
        # Piola-Kirchhoff S
        S = self.lam * trE * self.I_batch + 2 * self.mu * E_strain
        
        # First Piola-Kirchhoff P = F * S. Stress P:
        P = cp.matmul(Fs, S)
        
        # Compute Nodal Forces (Divergence of Stress) ---
        # Equilibrium Equation (Eq. 2): Div(P) + f_b = 0
        # We integrate Div(P) over the nodal volume to get the force.
        # Approximated via a Finite Volume-like approach on the graph.
        
        #(Divergence of Stress)
        P_i = P[self.row]
        P_j = P[self.col]
        
        #We approximate the stress at the interface between node i and j
        # as the average of their stress tensors.
        P_avg = (P_i + P_j) * 0.5
        
        
        dX_vec = dX_edges 
        
        # Project Stress tensor onto the geometric vector to get Force vector.
        # Corresponds to surface integral of Stress * Normal.
        f_contrib = cp.matmul(P_avg, dX_vec) 
        f_contrib = f_contrib.squeeze(-1)   
        
        #Accumulate force contributions from neighbors
        forces = cp.zeros_like(self.X)
        cupyx.scatter_add(forces, self.row, f_contrib)
        
        # Scaling finale
        return forces * (1.0 / (self.density))
        
        
    
    def get_acc(self, u_in, v_in,f_ext):
            """ Computes acceleration: a = (F_int + F_ext - Damping) / Mass """
            f_int = self.compute_forces(u_in)
            damping = -0.1 * v_in
            return f_int + f_ext + damping
        
        
    def update_euler(self,u,v,force_ext):
        """
        Node update function using Euler time integration.
        
        Args:
            node_features: Current [vx, vy, vz, fx, fy, fz] for each node
            
        Returns:
            new_features: Updated [vx, vy, vz, fx, fy, fz] for each node
        """
    
        #get_derivatives u,v,force_ext
        dv_dt = self.get_acc(u,v,force_ext)
        
        #Euler time integration
        v_new = v + self.dt * dv_dt
        
        #Semi-Implicit Euler Method:
        #in our case in theory we should substitute v_new with du_dt=v but in our case it gives us numerical explosion
        #but we can resolve problem applying directly new velocity estimation v_new to update the position. This can preserve system energy
        u_new = u + self.dt * v_new 

        
        #Enforce boundary conditions (all edges fixed)
        
        u_new[self.fixed_mask] = 0.0
        v_new[self.fixed_mask] = 0.0
        
        
        
        return u_new, v_new
    
    def update_rk4(self, u, v, f_ext):
        """
        Performs one time step using Runge-Kutta 4th order integration.
        
        """
        
        # RK4 Integration Steps (k1, k2, k3, k4)

        # First Step
        k1_v = self.get_acc(u, v,f_ext=f_ext)
        k1_u = v

        # Update u_t, v_t first step
        u_t_1 = u + 0.5 * self.dt * k1_u
        v_t_1 = v + 0.5 * self.dt * k1_v

        k2_v = self.get_acc(u_t_1, v_t_1,f_ext=f_ext)
        k2_u = v_t_1  

        # Update u_t, v_t second step
        u_t_2 = u + 0.5 * self.dt * k2_u
        v_t_2 = v + 0.5 * self.dt * k2_v
        
        k3_v = self.get_acc(u_t_2, v_t_2,f_ext)
        k3_u = v_t_2 # Correzione logica RK4 standard
        
        # Update u_t, v_t third step
        u_t_3 = u + self.dt * k3_u
        v_t_3 = v + self.dt * k3_v

        k4_v = self.get_acc(u_t_3, v_t_3,f_ext)
        k4_u = v_t_3
        
        # Weighted average of slopes to update state
        u_new = u + (self.dt / 6.0) * (k1_u + 2*k2_u + 2*k3_u + k4_u)
        v_new = v + (self.dt / 6.0) * (k1_v + 2*k2_v + 2*k3_v + k4_v)

        # Apply Boundary Conditions (Dirichlet)
        u_new[self.fixed_mask] = 0.0
        v_new[self.fixed_mask] = 0.0
        

        return u_new, v_new


def recvall(sock, n):
    """Helper to receive exactly n bytes"""
    data = b''
    while len(data) < n:
        packet = sock.recv(n - len(data))
        if not packet:
            return None
        data += packet
    return data

def receive_mesh_from_unity(conn):
    print("In attesa della geometria da Unity...")
    
    #Receive number of vertices (int - 4 bytes)
    data = recvall(conn, 4)
    if not data: return None
    num_verts = struct.unpack('<i', data)[0]
    print(f"Ricezione {num_verts} vertici...")
    
    #Receive all vertices(num_verts * 3 * 4 bytes)
    # 3 floats per vertice (x, y, z)
    data_verts = recvall(conn, num_verts * 3 * 4)

    vertices = np.frombuffer(data_verts, dtype=np.float32).reshape(num_verts, 3)
    
    #Riceive number of triangles (indexes) (int - 4 bytes)
    data = recvall(conn, 4)
    num_indices = struct.unpack('<i', data)[0]
    print(f"Ricezione {num_indices} indici (triangoli)...")
    
    #Indixes (num_indices * 4 bytes)
    data_indices = recvall(conn, num_indices * 4)
   
    indices = np.frombuffer(data_indices, dtype=np.int32)
    
    # Trimesh si aspetta facce come (N, 3), Unity invia una lista piatta di indici
    faces = indices.reshape(-1, 3)
    
    print("Mesh ricostruita con successo!")

    # Create mesh with process=False such that the received vertices don't transform their orientation/position in trimesh avoiding mismatch. 
    mesh = trimesh.Trimesh(vertices=vertices, faces=faces,process=False)
 
    return mesh

def setup_voxel_graph_from_mesh(visual_mesh, pitch):
    """
    Accept directly the virtual mesh 
    """
    print(f"Generazione Voxel Graph (Pitch={pitch})...")
    
    # Voxelization and it is used the fill method to ensure that it is a 3D volume
    voxel_grid = visual_mesh.voxelized(pitch=pitch).fill()
    phys_nodes = voxel_grid.points
    
    print(f"Generati {len(phys_nodes)} nodi fisici.")
    
    # Graph building
    tree = cKDTree(phys_nodes)
    radius = pitch * 1.5
    pairs = tree.query_pairs(r=radius)
    edges = np.array(list(pairs))
    
    #conmpite weights based on a distance
    print("Calcolo pesi Skinning...")
    dist, indices = tree.query(visual_mesh.vertices, k=4)
    weights = 1.0 / (dist + 1e-8)
    weights /= weights.sum(axis=1)[:, np.newaxis]
    
    return visual_mesh, phys_nodes, indices, weights, edges



def handle_client(conn, addr):
    """
    Handles physical simulation for a single connected muscle.
    This function runs in a parallel, independent thread.
    """
    print(f"[Client {addr}] Nuovo muscolo connesso!")
    #collection samples_time and rtt data
    max_samples = 300
    rtt_data = []

    #Connection between Client and server using TCP protocol
    try:
        with conn:
            conn.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
            
            #receive mesh 
            unity_mesh = receive_mesh_from_unity(conn)
            
            if unity_mesh is None:
                print(f"[Client {addr}] Errore ricezione mesh.")
                return

            params_data = recvall(conn, 20)
            if not params_data:
                print(f"[Client {addr}] Errore ricezione parametri fisici.")
                return
                
            PITCH, DT, YOUNGS_MODULUS, POISSON_RATIO, DENSITY = struct.unpack('<fffff', params_data)

            # setup physics
            visual_mesh, phys_nodes, skin_idx, skin_w, edges = setup_voxel_graph_from_mesh(unity_mesh, PITCH)
            
            
            phys_nodes_gpu = cp.asarray(phys_nodes, dtype=cp.float32)
            visual_mesh_vertex_gpu = cp.asarray(visual_mesh.vertices, dtype=cp.float32)
            skin_idx_gpu = cp.asarray(skin_idx)
            skin_w_gpu = cp.asarray(skin_w, dtype=cp.float32)
            edges_gpu = cp.asarray(edges)
            
            #Boundary Conditions
            coords_max = phys_nodes.max(axis=0)
            coords_min = phys_nodes.min(axis=0)
            long_axis = np.argmax(coords_max - coords_min)
            
            c_vals = phys_nodes[:, long_axis]
            length = c_vals.max() - c_vals.min()
            
            fixed_mask = (c_vals < (c_vals.min() + length*0.005)) | (c_vals > (c_vals.max() - length*0.005))
            fixed_mask_gpu = cp.asarray(fixed_mask)
            print(f"Lunghezza calcolata: {length}")
            print(f"Nodi totali: {len(phys_nodes)}")
            print(f"Nodi BLOCCATI: {np.sum(fixed_mask)}")
            
            solver = ContinuumGNN(phys_nodes_gpu, edges_gpu, fixed_mask_gpu, YOUNGS_MODULUS, POISSON_RATIO, DENSITY, DT)
            
            # Variabili di stato indipendenti per questo specifico muscolo
            u = cp.zeros_like(phys_nodes_gpu)
            v = cp.zeros_like(phys_nodes_gpu)
            f_ext = cp.zeros_like(phys_nodes_gpu)

            print(f"[Client {addr}] Simulazione avviata. Loop principale...")
            
            #simulation loop
            while True:
                
                #start process_start collection 
                process_start = time.perf_counter()

                #receive contact force and contact point
                data = recvall(conn, 24)
                if not data: 
                    print(f"[Client {addr}] Disconnesso.")
                    break
                
                fx, fy, fz, px, py, pz = struct.unpack('<ffffff', data)
                
               
                current_force_vec = cp.array([fx, fy, fz], dtype=cp.float32)
                contact_point = cp.array([px, py, pz], dtype=cp.float32)
                force_magnitude = cp.linalg.norm(current_force_vec)
                
                is_dragging = force_magnitude > 0.001
                
                if is_dragging:
                    #active modality
                    f_ext[:] = 0 
                    
                    current_nodes_pos = phys_nodes_gpu + u
                    dist_from_contact = cp.linalg.norm(current_nodes_pos - contact_point, axis=1)

                    #application of fallout to highlight the how force interacts with respect the contact point
                    falloff_radius = length * 0.35
                    falloff = cp.clip(1.0 - (dist_from_contact / falloff_radius), 0.0, 1.0)

                    f_ext += current_force_vec * 5.0 * falloff[:, None] 
                    
                    #execute deformation muscle with (Runge-Kutta integration method)
                    u, v = solver.update_rk4(u, v, f_ext)
                    #u, v = solver.update_euler(u, v, f_ext)
                     
                    #Constraint of max deformation distance
                    MAX_DEFORMATION = length * 0.2
                    u_norms = cp.linalg.norm(u, axis=1)
                    max_current_deformation = cp.max(u_norms)
                    
                    #fixed max position of vertices (u) and (velocity is 0.1*v)
                    if max_current_deformation > MAX_DEFORMATION:
                        scale_factor = MAX_DEFORMATION / max_current_deformation
                        u *= scale_factor 
                        v *= 0.1 
                    
                    v *= 0.98
                    
                else:
                    #when the object is not dragging -> positiona and velocity remains stable to zero.
                    v[:] = 0.0 
                    
                    
                    if cp.max(cp.abs(u)) < 0.0001:
                        u[:] = 0.0

                #Transmission new u weighted.
                u_neighbors = u[skin_idx_gpu]
                weighted_u = u_neighbors * skin_w_gpu[:, :, None]
                disp = cp.sum(weighted_u, axis=1)
                
                new_verts = visual_mesh_vertex_gpu + disp
                verts_cpu = cp.asnumpy(new_verts)
                
                verts_bytes = verts_cpu.tobytes()
                header = struct.pack('<I', len(verts_bytes))
                conn.sendall(header + verts_bytes)

                #collection of process end
                process_end = time.perf_counter()

                duration_ms = (process_end - process_start) * 1000
                rtt_data.append(duration_ms)

                
                if len(rtt_data) >= max_samples:
       
                    with open('performance_log_rk4.csv', 'w', newline='') as f:

                        writer = csv.writer(f)
                        writer.writerow(["Frame", "ProcessingTime_ms"])
                        for i, val in enumerate(rtt_data):
                            writer.writerow([i, val])

                    #print("Dati salvati in performance_log_euler121.csv!")
                    
                
    except Exception as e:
        print(f"[Client {addr}] Errore loop: {e}")

def start_server():
    HOST = '127.0.0.1'
    PORT = 65432
    
    print(f"Server MULTI-THREAD in ascolto su {HOST}:{PORT}")
    print("In attesa che i muscoli di Unity si connettano...")
    
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        #create the server socket and it listens for client connections
        s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        s.bind((HOST, PORT))
        s.listen()
        
        while True:
            # accept connection
            conn, addr = s.accept()
            
            #create and starts a thread separated for new muscle.
            client_thread = threading.Thread(target=handle_client, args=(conn, addr), daemon=True)
            client_thread.start()

if __name__ == "__main__":

    with warnings.catch_warnings():
        warnings.simplefilter("ignore")
    

    start_server()