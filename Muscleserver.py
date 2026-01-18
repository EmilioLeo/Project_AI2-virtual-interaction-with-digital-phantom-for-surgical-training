import socket
import struct
import numpy as np
import cupy as cp
import cupyx
import scipy.sparse as sp
import trimesh
from scipy.spatial import cKDTree
import time

# --- INCOLLA QUI LE TUE CLASSI setup_voxel_graph E ContinuumGNN DAL NOTEBOOK ---
# (Per brevità non le ricopio qui, ma sono essenziali affinché funzioni)
# ...

def setup_voxel_graph(filename, pitch, target_scale=100.0):
    """
    Loads an STL mesh, converts it into a volumetric voxel graph for physics,
    and computes interpolation weights for visualization.
    
    Args:
        filename (str): Path to the .stl file.
        pitch (float): Voxel size/resolution. Determines density of physical nodes.
        target_scale (float): Normalization scale for the mesh (default: 100.0).
        
    Returns:
        visual_mesh: The original trimesh object.
        phys_nodes: Array (N, 3) of physical node coordinates (centers of voxels).
        L: Sparse Matrix (N, N), the Graph Laplacian operator.
        indices: Array (M, 4), indices of 4 nearest physical nodes for each visual vertex.
        weights: Array (M, 4), interpolation weights for skinning.
    """
    print(f"Loading {filename}...")
    
    # 1. Load Visual Mesh
     # This mesh is used only for visualization, not for physics calculation.
    visual_mesh = trimesh.load(filename)
    
    # Mesh Simplification 
    # Reduces the number of triangles to improve performance in Matplotlib.
    target_faces = 2500 
    if visual_mesh.faces.shape[0] > target_faces:
        print(f"Mesh too complex ({visual_mesh.faces.shape[0]} faces). Simplifying to ~{target_faces}...")
        try:
            # simplify_quadric_decimation preserves the shape while reducing face count.
            visual_mesh = visual_mesh.simplify_quadric_decimation(target_faces)
            print(f"Simplification successful: {visual_mesh.faces.shape[0]} faces.")
        except Exception as e:
            print(f"Warning: Mesh simplification failed ({e}). Visualization might be slow.")

    #print("Salvataggio mesh sincronizzata per Unity...")
    #visual_mesh.export('mesh_simplified.obj')
    #print("Export completato: 'mesh_sincronizzata.obj'")
    #Scaling
    # Normalizes the mesh size to a target scale (100 units) to ensure numerical stability for physics.
    #scale_factor = target_scale / visual_mesh.extents.max()
    #visual_mesh.apply_scale(scale_factor)
    
    # 2. Physical Voxelization 
    print(f"Physical Voxelization (Pitch={pitch})...")
    
    # Creates a grid of solid voxels inside the mesh volume.
    # 'pitch' determines the distance between nodes (resolution).
    voxel_grid = visual_mesh.voxelized(pitch=pitch)

    # Extract the center points of the voxels. These act as the mass particles for the simulation.
    phys_nodes = voxel_grid.points
    print(f"Generated {len(phys_nodes)} physical nodes.")
    
    # 3. Graph Construction
    # Uses a KDTree to find neighboring particles within a specific radius.
    tree = cKDTree(phys_nodes)
    
    # Connection radius: pitch * 1.5 ensures connectivity with diagonals (structural stability).
    radius = pitch * 1.5
    pairs = tree.query_pairs(r=radius)
    edges = np.array(list(pairs)) 
    
    # Build the Sparse Adjacency Matrix and Laplacian
    # Create row and col indices for bidirectional connections (undirected graph).
    row = np.concatenate([edges[:, 0], edges[:, 1]])
    col = np.concatenate([edges[:, 1], edges[:, 0]])
    data = np.ones(len(row))
    
    num_phys = len(phys_nodes)
    # Sparse Coordinate Matrix representing the graph connectivity (1 if connected, 0 otherwise).
    A = sp.coo_matrix((data, (row, col)), shape=(num_phys, num_phys))
    
    # Degree Matrix (D): Diagonal matrix where D_ii is the number of neighbors for node i.
    degrees = np.array(A.sum(axis=1)).flatten()
    degrees[degrees == 0] = 1 # Avoid division by zero for isolated nodes
    D = sp.diags(degrees)

    # Graph Laplacian (L = D - A).
    # This matrix operator approximates the spatial second derivative (divergence of gradient).
    L = D - A
    
    # 4. Compute Skinning Weights
    print("Computing Skinning weights...")
    # Find the 4 closest physical nodes for every vertex of the visual mesh.
    dist, indices = tree.query(visual_mesh.vertices, k=4)
    
    
    # Inverse Distance Weighting: Closer nodes have more influence on the vertex movement.
    weights = 1.0 / (dist + 1e-8) # Add epsilon to avoid division by zero
    
    # Normalize weights so they sum to 1.0.
    weights /= weights.sum(axis=1)[:, np.newaxis]
    
    
    return visual_mesh, phys_nodes, L.tocsr(), indices, weights, edges

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
        return forces * (1.0 / (self.density * 100.0))
        
        
    
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


# function to recvall from 12 bytes 
def recvall(sock, n):
    """Legge esattamente n byte dal socket o restituisce None se EOF."""
    data = b''
    while len(data) < n:
        packet = sock.recv(n - len(data))
        if not packet:
            return None
        data += packet
    return data


#CREATION SERVER

def start_server():
    # --- CONFIGURAZIONE ---
    HOST = '127.0.0.1'
    PORT = 65432
    filename = "Prototipo_Muscolo Stern_DX.stl" # Assicurati che sia lo stesso modello usato in Unity!
    PITCH = 4.0
    DT = 0.001 #before 0.005
    YOUNGS_MODULUS = 600.0 #before 1500
    POISSON_RATIO = 0.4 #before 0.4
    DENSITY = 2.0
    method='rk4' #euler
    print("Inizializzazione Fisica...")
    # 1. Setup Voxel Graph
    visual_mesh, phys_nodes, L, skin_idx, skin_w, edges = setup_voxel_graph(filename, PITCH)
    
    # Setup GPU Data
    phys_nodes_gpu = cp.asarray(phys_nodes)
    visual_mesh_vertex_gpu = cp.asarray(visual_mesh.vertices, dtype=cp.float32)
    skin_idx_gpu = cp.asarray(skin_idx)
    skin_w_gpu = cp.asarray(skin_w)
    edges_gpu = cp.asarray(edges)
    
    num_verts_python = visual_mesh_vertex_gpu.shape[0]
    print(f"PYTHON: Sto gestendo {num_verts_python} vertici.")
    print(f"PYTHON: Ogni pacchetto sarà di {num_verts_python * 3 * 4} bytes.")
    
    # Boundary Conditions (Esempio: estremi fissi sull'asse lungo)
    extent = phys_nodes.max(axis=0) - phys_nodes.min(axis=0)
    long_axis = np.argmax(extent)
    coords = phys_nodes[:, long_axis]
    length = coords.max() - coords.min()
    fixed_mask = (coords < (coords.min() + length*0.1)) | (coords > (coords.max() - length*0.1))
    fixed_mask_gpu = cp.asarray(fixed_mask)
    num_fixed = cp.sum(fixed_mask_gpu).item()
    print(f"DEBUG: Numero di nodi fissati: {num_fixed} su {len(phys_nodes)}")
    # Inizializza Solver
    solver = ContinuumGNN(phys_nodes_gpu, edges_gpu, fixed_mask_gpu, YOUNGS_MODULUS, POISSON_RATIO, DENSITY, DT)
    
    # Stato Iniziale
    u = cp.zeros_like(phys_nodes_gpu)
    v = cp.zeros_like(phys_nodes_gpu)
    f_ext = cp.zeros_like(phys_nodes_gpu) # Forza esterna dinamica

    # Maschera per applicare la forza (es. centro del muscolo)
    muscle_center = np.mean(phys_nodes, axis=0)
    dist_from_center = np.linalg.norm(phys_nodes - muscle_center, axis=1)
    force_application_mask = cp.asarray(dist_from_center < (length * 0.2))

    print(f"Server pronto su {HOST}:{PORT}. In attesa di Unity...")

    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.bind((HOST, PORT))
        s.listen()
        conn, addr = s.accept()
        with conn:
            print(f"Connesso a {addr}")
            
            # Disabilita Nagle's algorithm per bassa latenza
            conn.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)

            try:
                while True:
                    # 1. RICEVI INPUT DA UNITY (Vector3 forza: x, y, z) -> 12 bytes
                    data =recvall(conn,12)
                    if not data: break
                    
                    # Unpack 3 floats
                    fx, fy, fz = struct.unpack('<fff', data)
                    print(f"force: ({fx}, {fy} ,{fz})")
                    # Aggiorna il vettore forza esterna in base all'input del mouse
                    # Unity è asse Y-up, Python/Trimesh spesso Z-up. Potrebbe servire uno swap.
                    # Qui assumiamo coordinate mappate 1:1, aggiusta se necessario.
                    current_force = cp.array([fx, fy, fz])
                    
                    f_ext[:] = 0
                    f_ext[force_application_mask] = current_force

                    # 2. STEP FISICO
                    if method=='rk4':
                        u, v = solver.update_rk4(u, v, f_ext)
                    else:
                        u, v = solver.update_euler(u,v,f_ext)
                    
                    print(f"new position {u} and new velocity {v}")
                    # 3. SKINNING (Calcolo nuovi vertici visuali)
                    weighted_disp = cp.sum(u[skin_idx_gpu] * skin_w_gpu[:, :, None], axis=1)
                    new_visual_verts = visual_mesh_vertex_gpu + weighted_disp
                    
                    # 4. INVIA DATI A UNITY
                    # Converti in numpy CPU per l'invio
                    verts_cpu = cp.asnumpy(new_visual_verts)
                   
                    # Flatten array e pack in bytes (N * 3 * float)
                    verts_bytes = verts_cpu.astype(np.float32).tobytes()
                    size= len(verts_bytes)
                    
                    #Creation Header
                    header = struct.pack('<I', size)
                    
                    # Invia prima la lunghezza del pacchetto (opzionale, ma sicuro) o invia direttamente
                    conn.sendall(header + verts_bytes)

            except Exception as e:
                print(f"Errore: {e}")

if __name__ == "__main__":
    start_server()