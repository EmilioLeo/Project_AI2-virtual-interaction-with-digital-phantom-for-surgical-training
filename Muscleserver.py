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

import warnings

# --- INCOLLA QUI LE TUE CLASSI setup_voxel_graph E ContinuumGNN DAL NOTEBOOK ---
# (Per brevità non le ricopio qui, ma sono essenziali affinché funzioni)
# ...


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
    
        self.X = nodes.copy() # Material Coordinates X (Static Reference)
        self.num_nodes = len(nodes)
        self.fixed_mask = fixed_mask
        self.dt = dt
        self.density = density
        
        # --- Constitutive Parameters (Lamé Constants) ---
        # Converted from Young's Modulus (E) and Poisson's Ratio (nu).
        # These define the linear elastic response within the St. Venant-Kirchhoff model.
        self.mu = E / (2 * (1 + nu))           # Second Lamé parameter (Shear Modulus)
        self.lam = (E * nu) / ((1 + nu) * (1 - 2 * nu)) # First Lamé parameter (Bulk Modulus)
        
        # --- Precomputing Reference Gradients (Meshless Shape Functions) ---
        # To calculate the Deformation Gradient F = dx/dX, we need to approximate spatial 
        # derivatives on a point cloud. We use a Moving Least Squares (MLS) approach.
        # We precompute the inverse of the "Moment Matrix" (Covariance of positions)
        # in the Reference Configuration.
        
        print("Precomputing Reference Gradients (Inverse Shape Matrices)...")
        
        # Build adjacency list for fast neighbor lookup
        self.neighbors = [[] for _ in range(self.num_nodes)]
        for e in edges:
            self.neighbors[e[0]].append(e[1])
            self.neighbors[e[1]].append(e[0])
            
        # Storage for Inverse Moment Matrices B_inv (3x3 for each node)
        # Mathematical basis: F_i * Sum((X_j - X_i)*(X_j - X_i)^T) = Sum((x_j - x_i)*(X_j - X_i)^T)
        self.inv_moments = np.zeros((self.num_nodes, 3, 3))
        self.valid_deriv = np.ones(self.num_nodes, dtype=bool)
        
        for i in range(self.num_nodes):
            # Get neighbors in Reference Configuration
            neigh_idxs = self.neighbors[i]
            
            # We need at least 3 neighbors to resolve a 3D gradient
            if len(neigh_idxs) < 3: 
                self.valid_deriv[i] = False
                continue
            
            # dX: Vector from node i to neighbors j in Reference configuration
            dX = self.X[neigh_idxs] - self.X[i] # Shape (K, 3)
            
            # Compute Moment Matrix M = Sum (dX * dX^T)
            # This represents the geometric distribution of neighbors.
            M = np.dot(dX.T, dX)
            
            # Regularization: Add epsilon to diagonal to prevent singular matrix inversion
            M = M + np.eye(3) * 1e-9
            
            try:
                self.inv_moments[i] = np.linalg.inv(M)
            except np.linalg.LinAlgError:
                self.valid_deriv[i] = False
    """
    
        self.X = cp.asarray(nodes, dtype=cp.float32) # Material Coordinates X (Static Reference)
        self.num_nodes = self.X.shape[0]
        self.fixed_mask = cp.asarray(fixed_mask)
        self.dt = dt
        self.density = density
        
        # --- Constitutive Parameters (Lamé Constants) ---
        # Converted from Young's Modulus (E) and Poisson's Ratio (nu).
        # These define the linear elastic response within the St. Venant-Kirchhoff model.
        self.mu = E / (2 * (1 + nu))           # Second Lamé parameter (Shear Modulus)
        self.lam = (E * nu) / ((1 + nu) * (1 - 2 * nu)) # First Lamé parameter (Bulk Modulus)
        
         # --- Precomputing Reference Gradients (Meshless Shape Functions) ---
        # To calculate the Deformation Gradient F = dx/dX, we need to approximate spatial 
        # derivatives on a point cloud. We use a Moving Least Squares (MLS) approach.
        # We precompute the inverse of the "Moment Matrix" (Covariance of positions)
        # in the Reference Configuration.
        
        print("Precomputing Reference Gradients (Inverse Shape Matrices)...")
        
        #  Graph Topology Setup (neighbour lookup)
        # build bidirectional arcs
        # if edges is [[0,1]], we set [[0,1], [1,0]] to avoid loop in GPU 
        
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
        
        # Sommiamo i contributi M su ogni nodo (Scatter Add)
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
        
        
        """
        CPU code
        forces = np.zeros_like(current_x)
        
        # Note: Iterating nodes is computationally expensive in Python; 
        # strictly for educational clarity of the algorithm.
        for i in range(self.num_nodes):
            if not self.valid_deriv[i]: 
                Fs[i] = np.eye(3) # Identity: No deformation
                continue
                
            idx = self.neighbors[i]
            
            # Relative vectors in Current (deformed) and Reference configurations
            dx = current_x[idx] - current_x[i] # (K, 3)
            dX = self.X[idx] - self.X[i]       # (K, 3)
            
            # Least Squares approximation of F:
            # F = (Sum dx * dX^T) * M_inv
            numerator = np.dot(dx.T, dX) 
            Fs[i] = np.dot(numerator, self.inv_moments[i])

        """
        
        #GPU-ORIENTED Compute Deformation Gradient F (Eq. 6)
        
        # --- 1. Compute Deformation Gradient F (Eq. 6) ---
        # F maps vectors from Reference (X) to Current (x) configuration: dx = F * dX.
        # F captures stretch, shear, and rotation.
        #Fs = np.zeros((self.num_nodes, 3, 3))
        
        dx_edges = current_x[self.col] - current_x[self.row] # (N_edges, 3)
        dX_edges = self.X[self.col] - self.X[self.row]       # (N_edges, 3)
        
        # Reshape for matrix multiplication (N, 3, 1)
        dx_edges = dx_edges[..., None]
        dX_edges = dX_edges[..., None]
        
        # Least Squares approximation of F:
        # F = (Sum dx * dX^T) * M_inv
        
        # Numerator contribution: dx * dX^T -> (N_edges, 3, 3)
        num_contrib = cp.matmul(dx_edges, dX_edges.transpose(0, 2, 1))
        
        # Somma sui nodi (Scatter)
        numerator = cp.zeros((self.num_nodes, 3, 3), dtype=cp.float32)
        cupyx.scatter_add(numerator, self.row, num_contrib)
        
        
        #F = Numerator * M_inv -> (N_nodes, 3, 3)
        Fs = cp.matmul(numerator, self.inv_moments)
        
        # --- 2. Compute Stress Tensor P (Constitutive Law) ---
        # We implement a Saint Venant-Kirchhoff material. 
        # This is a "Cauchy Elastic" model extended to large deformations.
        
        # Green-Lagrange Strain Tensor E:
        # E = 0.5 * (F^T * F - I)
        # Crucial: E is invariant to rigid body rotations, solving the issue 
        # of linear elasticity mentioned in the prompt.
        
        """
        FT = np.transpose(Fs, axes=(0, 2, 1))
        FTF = np.matmul(FT, Fs)
        I = np.eye(3)
        E_strain = 0.5 * (FTF - I[np.newaxis, :, :])
        
        # Trace of Strain Tensor (Volumetric strain approximation)
        trE = np.trace(E_strain, axis1=1, axis2=2)[:, np.newaxis, np.newaxis]
        
        # Second Piola-Kirchhoff Stress S:
        # Relates to the reference configuration, independent of rotation.
        # S = lambda * tr(E) * I + 2 * mu * E
        S = self.lam * trE * I + 2 * self.mu * E_strain
        
        # First Piola-Kirchhoff Stress P (Eq. 5 context):
        # P = F * S
        # P relates forces in the current configuration to areas in the reference configuration.
        P = np.matmul(Fs, S)
        """
        
        #PART GPU-oriented  (Compute Stress Tensor P (Constitutive Law))
        
        FT = Fs.transpose(0, 2, 1)
        FTF = cp.matmul(FT, Fs)
        E_strain = 0.5 * (FTF - self.I_batch)
        
        # Trace of E (sum of diagonals) -> (N, 1, 1) per broadcasting
        trE = cp.trace(E_strain, axis1=1, axis2=2)[:, None, None]
        
        # Piola-Kirchhoff S
        S = self.lam * trE * self.I_batch + 2 * self.mu * E_strain
        
        # First Piola-Kirchhoff P = F * S. Stress P (Eq. 5 context):
        P = cp.matmul(Fs, S)
        
        # --- 3. Compute Nodal Forces (Divergence of Stress) ---
        # Equilibrium Equation (Eq. 2): Div(P) + f_b = 0
        # We integrate Div(P) over the nodal volume to get the force.
        # Approximated via a Finite Volume-like approach on the graph.
        """
        for i in range(self.num_nodes):
            idx = self.neighbors[i]
            for j_idx, j in enumerate(idx):
                # Vector connecting nodes in Reference configuration (Area normal direction)
                dX_vec = self.X[j] - self.X[i] 
                
                # We approximate the stress at the interface between node i and j
                # as the average of their stress tensors.
                P_avg = (P[i] + P[j]) * 0.5
                
                # Project Stress tensor onto the geometric vector to get Force vector.
                # Corresponds to surface integral of Stress * Normal.
                f_contribution = np.dot(P_avg, dX_vec) 
                
                # Accumulate force contributions from neighbors
                forces[i] += f_contribution

        # Normalize forces by density/volume factor to align with acceleration scale
        return forces * (1.0 / (self.density * 100.0))
        """
        
        #Part GPU ORIENTED (Divergence of Stress)
        P_i = P[self.row]
        P_j = P[self.col]
        
        #We approximate the stress at the interface between node i and j
        # as the average of their stress tensors.
        P_avg = (P_i + P_j) * 0.5
        
        # Vettore normale (reference configuration) dX
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
            node_features: Current [vx, vy, vz, fx, fy, fz] for each node, shape (Nx, Ny, Nz, 9)
            
        Returns:
            new_features: Updated [vx, vy, vz, fx, fy, fz] for each node, shape (Nx, Ny, Nz, 9)
        """
        
        #Extract current state
        #u = node_features[:, :,  0] #current position of all nodes
        #v = node_features[:, :,  1] #current velocity of all nodes 
        #force_ext = node_features[:, :, 2] #current external forces influence all nodes
        
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
    """Helper per ricevere esattamente n bytes"""
    data = b''
    while len(data) < n:
        packet = sock.recv(n - len(data))
        if not packet:
            return None
        data += packet
    return data

def receive_mesh_from_unity(conn):
    print("In attesa della geometria da Unity...")
    
    # 1. Ricevi numero vertici (int - 4 bytes)
    data = recvall(conn, 4)
    if not data: return None
    num_verts = struct.unpack('<i', data)[0]
    print(f"Ricezione {num_verts} vertici...")
    
    # 2. Ricevi array vertici (num_verts * 3 * 4 bytes)
    # 3 floats per vertice (x, y, z)
    data_verts = recvall(conn, num_verts * 3 * 4)
    # Converti bytes in array numpy di float32
    # Nota: Unity invia Little Endian
    vertices = np.frombuffer(data_verts, dtype=np.float32).reshape(num_verts, 3)
    
    # 3. Ricevi numero indici triangoli (int - 4 bytes)
    data = recvall(conn, 4)
    num_indices = struct.unpack('<i', data)[0]
    print(f"Ricezione {num_indices} indici (triangoli)...")
    
    # 4. Ricevi array indici (num_indices * 4 bytes)
    data_indices = recvall(conn, num_indices * 4)
    # Converti in array numpy di int32
    indices = np.frombuffer(data_indices, dtype=np.int32)
    
    # Trimesh si aspetta facce come (N, 3), Unity invia una lista piatta di indici
    faces = indices.reshape(-1, 3)
    
    print("Mesh ricostruita con successo!")
    #print(f"[PYTHON] Ricevuti {num_verts} vertici.")
    #print(f"[PYTHON] Primo vertice ricevuto (ID:0): {vertices[0]}")
    # Creiamo l'oggetto Trimesh
    mesh = trimesh.Trimesh(vertices=vertices, faces=faces,process=False)
    
    # Unity è Y-up (Left Handed), Trimesh solitamente non cambia nulla se non richiesto.
    # Dato che inviamo i vertici raw da Unity e li rimandiamo indietro raw, 
    # NON dovremmo aver bisogno di rotazioni se lavoriamo in coordinate locali.
    
    return mesh

def setup_voxel_graph_from_mesh(visual_mesh, pitch):
    """
    Versione modificata che accetta direttamente l'oggetto trimesh
    invece di caricare da file.
    """
    print(f"Generazione Voxel Graph (Pitch={pitch})...")
    
    # Voxelizzazione
    # Usiamo il metodo fill per assicurarci che sia solido
    voxel_grid = visual_mesh.voxelized(pitch=pitch).fill()
    phys_nodes = voxel_grid.points
    
    print(f"Generati {len(phys_nodes)} nodi fisici.")
    
    # Costruzione Grafo (cKDTree)
    tree = cKDTree(phys_nodes)
    radius = pitch * 1.5
    pairs = tree.query_pairs(r=radius)
    edges = np.array(list(pairs))
    
    # ... calcolo pesi skinning ...
    print("Calcolo pesi Skinning...")
    dist, indices = tree.query(visual_mesh.vertices, k=4)
    weights = 1.0 / (dist + 1e-8)
    weights /= weights.sum(axis=1)[:, np.newaxis]
    
    return visual_mesh, phys_nodes, indices, weights, edges

"""
def start_server():
    HOST = '127.0.0.1'
    PORT = 65432
    
    # Parametri Fisici
    PITCH = 0.04  # Regola in base alla scala di Unity! (es. se muscolo è 1 metro, 0.04 va bene)
    DT = 0.001
    YOUNGS_MODULUS = 5000.0
    POISSON_RATIO = 0.45
    DENSITY = 1000.0
    
    print(f"Server in ascolto su {HOST}:{PORT}")
    
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.bind((HOST, PORT))
        s.listen()
        conn, addr = s.accept()
        
        with conn:
            print(f"Connesso a {addr}")
            conn.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
            
            # --- FASE 1: RICEZIONE MESH ---
            # Prima di tutto, riceviamo la mesh da Unity
            unity_mesh = receive_mesh_from_unity(conn)
            
            if unity_mesh is None:
                print("Errore ricezione mesh.")
                return

            # --- FASE 2: SETUP FISICA ---
            visual_mesh, phys_nodes, skin_idx, skin_w, edges = setup_voxel_graph_from_mesh(unity_mesh, PITCH)
            
            # Setup GPU
            phys_nodes_gpu = cp.asarray(phys_nodes, dtype=cp.float32)
            visual_mesh_vertex_gpu = cp.asarray(visual_mesh.vertices, dtype=cp.float32)
            skin_idx_gpu = cp.asarray(skin_idx)
            skin_w_gpu = cp.asarray(skin_w, dtype=cp.float32)
            edges_gpu = cp.asarray(edges)
            
            # Boundary Conditions (Fissiamo gli estremi sull'asse Y come esempio)
            # Nota: Unity ha coordinate diverse, controlla quale asse è quello lungo
            coords_max = phys_nodes.max(axis=0)
            coords_min = phys_nodes.min(axis=0)
            long_axis = np.argmax(coords_max - coords_min)
            
            c_vals = phys_nodes[:, long_axis]
            length = c_vals.max() - c_vals.min()
            fixed_mask = (c_vals < (c_vals.min() + length*0.005)) | (c_vals > (c_vals.max() - length*0.005))
            fixed_mask_gpu = cp.asarray(fixed_mask)
            
            solver = ContinuumGNN(phys_nodes_gpu, edges_gpu, fixed_mask_gpu, YOUNGS_MODULUS, POISSON_RATIO, DENSITY, DT)
            
            u = cp.zeros_like(phys_nodes_gpu)
            v = cp.zeros_like(phys_nodes_gpu)
            f_ext = cp.zeros_like(phys_nodes_gpu)
            
            # Maschera forza mouse
            center_gpu = cp.mean(phys_nodes_gpu, axis=0)
            dist_from_center = cp.linalg.norm(phys_nodes_gpu - center_gpu, axis=1)
            #force_mask = dist_from_center < (length * 0.2)

            print("Simulazione avviata. Loop principale...")
            
            # --- FASE 3: LOOP SIMULAZIONE ---
            #i=0
            try:
                while True:
                    # 1. RICEZIONE INPUT DA UNITY
                    data = recvall(conn, 24)
                    if not data: break
                    
                    fx, fy,fz, px, py,pz = struct.unpack('<ffffff', data)
                    
                    # Vettore forza attuale
                    current_force_vec = cp.array([fx, fy,fz], dtype=cp.float32)
                    contact_point = cp.array([px, py,pz], dtype=cp.float32)
                    force_magnitude = cp.linalg.norm(current_force_vec)
                    
                    # SOGLIA DI ATTIVAZIONE
                    # Se la forza è quasi zero, consideriamo che l'utente ha rilasciato il mouse
                    is_dragging = force_magnitude > 0.001
                    
                   
                    
                    if is_dragging:
                        # --- MODALITÀ ATTIVA (Utente trascina) ---


                        f_ext[:] = 0 
                        
                        # Calcoliamo la posizione *attuale* dei nodi (riposo + deformazione)
                        current_nodes_pos = phys_nodes_gpu + u
                        # Distanza dal dito WEART invece che dal centro statico
                        dist_from_contact = cp.linalg.norm(current_nodes_pos - contact_point, axis=1)

                        # Creiamo un'area di influenza della forza più sfumata (falloff gaussiano/lineare)
                        falloff_radius = length * 0.35 # Raggio di influenza (35% della lunghezza)
                        
                        # Calcola un decadimento: 1.0 al centro esatto, sfuma fino a 0.0 al limite del raggio
                        #falloff = cp.clip(1.0 - (dist_from_center / falloff_radius), 0.0, 1.0)
                        falloff = cp.clip(1.0 - (dist_from_contact / falloff_radius), 0.0, 1.0)

                        # Poiché ai lati la forza è minore, aumentiamo leggermente il moltiplicatore base 
                        # (da 5.0 a 10.0) per mantenere un bello spostamento
                        f_ext += current_force_vec * 5.0 * falloff[:, None] 
                        
                        # Esegui la fisica GNN completa (RK4)
                        u, v = solver.update_rk4(u, v, f_ext)
                         
                        # --- NUOVO: VINCOLO DI DISTANZA MASSIMA ---
                        # Imposta la deformazione massima consentita (es. 40% della lunghezza del muscolo)
                        MAX_DEFORMATION = length * 0.2
                        
                        # Calcola di quanto si è spostato ogni nodo dalla posizione di riposo
                        u_norms = cp.linalg.norm(u, axis=1)
                        max_current_deformation = cp.max(u_norms)
                        # Trova i nodi che hanno superato il limite
                        #exceed_mask = u_norms > MAX_DEFORMATION
                        
                        # Se almeno un nodo ha superato il limite...
                        if max_current_deformation > MAX_DEFORMATION:

                            # Normalizza il vettore di spostamento e forzalo alla lunghezza MAX_DEFORMATION
                            # Calcola un fattore di riduzione per farlo rientrare esattamente nel limite
                            scale_factor = MAX_DEFORMATION / max_current_deformation
                            u *= scale_factor

                            # Azzera (o quasi) la velocità di tutto il muscolo.
                            # La forza del mouse continuerà a tirare, ma il muscolo non accumulerà energia
                            v *= 0.1 
                        
                        # Smorzamento globale della velocità per stabilità
                        v *= 0.98

                    
                        
                    else:
                        # --- MODALITÀ RITORNO (Nessuna forza) ---
                        # Invece di calcolare la fisica, smorziamo tutto per tornare alla forma originale.
                        # Questo impedisce al muscolo di muoversi da solo o deformarsi all'avvio.
                        
                        # 1. Azzera istantaneamente la velocità per fermare oscillazioni
                        v[:] = 0.0 
                        
                        # 2. Interpolazione lineare verso 0 (Ritorno elastico geometrico)
                        # Moltiplichiamo u per 0.85 ogni frame: torna a zero velocemente ma fluidamente.
                        #u *= 0.85 
                        
                        # 3. Se lo spostamento è infinitesimale, azzera tutto per risparmiare calcoli
                        if cp.max(cp.abs(u)) < 0.0001:
                            u[:] = 0.0

                    # 3. SKINNING E INVIO (Identico a prima)
                    # Calcola la nuova posizione visiva basata su u
                    u_neighbors = u[skin_idx_gpu]
                    weighted_u = u_neighbors * skin_w_gpu[:, :, None]
                    disp = cp.sum(weighted_u, axis=1)
                    
                    # Posizione finale = Vertici Originali + Spostamento
                    new_verts = visual_mesh_vertex_gpu + disp
                    
                    # Serializza e invia
                    verts_cpu = cp.asnumpy(new_verts)
                    #print(f"[PYTHON] Invio aggiornamento. Primo vertice (ID:0): {verts_cpu[0]}")
                    verts_bytes = verts_cpu.tobytes()
                    header = struct.pack('<I', len(verts_bytes))
                    conn.sendall(header + verts_bytes)
                    #i+=1
                    
            except Exception as e:
                print(f"Errore loop: {e}")

if __name__ == "__main__":


    # 1. Ignoriamo momentaneamente l'avviso per vedere se il calcolo va a buon fine
    with warnings.catch_warnings():
        warnings.simplefilter("ignore")
    
    # 2. Proviamo a creare un array sulla GPU
        try:
            a = cp.array([10, 20, 30])
            print("✅ SUCCESSO! L'array è stato creato:", a)
            print("La tua GPU è:", cp.cuda.Device())
        except Exception as e:
            print("❌ ERRORE REALE:", e)

    start_server()

"""

def handle_client(conn, addr):
    """
    Gestisce la simulazione fisica per un singolo muscolo connesso.
    Questa funzione viene eseguita in un thread parallelo e indipendente.
    """
    print(f"[Client {addr}] Nuovo muscolo connesso!")
    
    # Parametri Fisici
    #PITCH = 0.02  
    #DT = 0.001
    #YOUNGS_MODULUS = 5000.0
    #POISSON_RATIO = 0.45
    #DENSITY = 1000.0
    
    try:
        with conn:
            conn.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
            
            # --- FASE 1: RICEZIONE MESH ---
            unity_mesh = receive_mesh_from_unity(conn)
            
            if unity_mesh is None:
                print(f"[Client {addr}] Errore ricezione mesh.")
                return

            params_data = recvall(conn, 20)
            if not params_data:
                print(f"[Client {addr}] Errore ricezione parametri fisici.")
                return
                
            PITCH, DT, YOUNGS_MODULUS, POISSON_RATIO, DENSITY = struct.unpack('<fffff', params_data)
            # --- FASE 2: SETUP FISICA ---
            visual_mesh, phys_nodes, skin_idx, skin_w, edges = setup_voxel_graph_from_mesh(unity_mesh, PITCH)
            
            # Setup GPU
            phys_nodes_gpu = cp.asarray(phys_nodes, dtype=cp.float32)
            visual_mesh_vertex_gpu = cp.asarray(visual_mesh.vertices, dtype=cp.float32)
            skin_idx_gpu = cp.asarray(skin_idx)
            skin_w_gpu = cp.asarray(skin_w, dtype=cp.float32)
            edges_gpu = cp.asarray(edges)
            
            # Boundary Conditions
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
            
            # --- FASE 3: LOOP SIMULAZIONE ---
            while True:
                # 1. RICEZIONE INPUT DA UNITY
                data = recvall(conn, 24)
                if not data: 
                    print(f"[Client {addr}] Disconnesso.")
                    break
                
                fx, fy, fz, px, py, pz = struct.unpack('<ffffff', data)
                
                # Vettore forza attuale e punto di contatto
                current_force_vec = cp.array([fx, fy, fz], dtype=cp.float32)
                contact_point = cp.array([px, py, pz], dtype=cp.float32)
                force_magnitude = cp.linalg.norm(current_force_vec)
                
                is_dragging = force_magnitude > 0.001
                
                if is_dragging:
                    # --- MODALITÀ ATTIVA ---
                    f_ext[:] = 0 
                    
                    current_nodes_pos = phys_nodes_gpu + u
                    dist_from_contact = cp.linalg.norm(current_nodes_pos - contact_point, axis=1)

                    falloff_radius = length * 0.35
                    falloff = cp.clip(1.0 - (dist_from_contact / falloff_radius), 0.0, 1.0)

                    f_ext += current_force_vec * 5.0 * falloff[:, None] 
                    
                    # Esegui la fisica
                    u, v = solver.update_rk4(u, v, f_ext)
                     
                    # VINCOLO DI DISTANZA MASSIMA
                    MAX_DEFORMATION = length * 0.2
                    u_norms = cp.linalg.norm(u, axis=1)
                    max_current_deformation = cp.max(u_norms)
                    
                    if max_current_deformation > MAX_DEFORMATION:
                        scale_factor = MAX_DEFORMATION / max_current_deformation
                        u *= scale_factor
                        v *= 0.1 
                    
                    v *= 0.98
                    
                else:
                    # --- MODALITÀ RITORNO ---
                    v[:] = 0.0 
                    
                    # Ritorno elastico: ho scommentato questa riga affinché il muscolo torni indietro!
                   # u *= 0.85 
                    
                    if cp.max(cp.abs(u)) < 0.0001:
                        u[:] = 0.0

                # 3. SKINNING E INVIO
                u_neighbors = u[skin_idx_gpu]
                weighted_u = u_neighbors * skin_w_gpu[:, :, None]
                disp = cp.sum(weighted_u, axis=1)
                
                new_verts = visual_mesh_vertex_gpu + disp
                verts_cpu = cp.asnumpy(new_verts)
                
                verts_bytes = verts_cpu.tobytes()
                header = struct.pack('<I', len(verts_bytes))
                conn.sendall(header + verts_bytes)
                
    except Exception as e:
        print(f"[Client {addr}] Errore loop: {e}")

def start_server():
    HOST = '127.0.0.1'
    PORT = 65432
    
    print(f"Server MULTI-THREAD in ascolto su {HOST}:{PORT}")
    print("In attesa che i muscoli di Unity si connettano...")
    
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        # SO_REUSEADDR evita l'errore se riavvii subito il server dopo averlo chiuso
        s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        s.bind((HOST, PORT))
        s.listen()
        
        while True:
            # Aspetta una connessione da Unity
            conn, addr = s.accept()
            
            # Crea e avvia un Thread separato per il nuovo muscolo
            # daemon=True chiude i thread automaticamente se interrompi il server principale
            client_thread = threading.Thread(target=handle_client, args=(conn, addr), daemon=True)
            client_thread.start()

if __name__ == "__main__":


    # 1. Ignoriamo momentaneamente l'avviso per vedere se il calcolo va a buon fine
    with warnings.catch_warnings():
        warnings.simplefilter("ignore")
    

    start_server()