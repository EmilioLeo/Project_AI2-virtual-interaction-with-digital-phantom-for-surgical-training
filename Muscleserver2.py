# ... (Classi GNN e setup_voxel_graph rimangono invariati) ...

def start_server():
    HOST = '127.0.0.1'
    PORT = 65432
    
    # PARAMETRI CALIBRATI PER SCALA 100
    # Se il muscolo in Unity è lungo "100 unità", un pitch di 5.0 crea un nodo ogni 5 unità.
    PITCH = 5.0  
    DT = 0.002
    YOUNGS_MODULUS = 1500.0
    POISSON_RATIO = 0.45
    DENSITY = 1000.0
    
    # ... (Parte di connessione e receive_mesh_from_unity invariata) ...
    
    try:
        while True:
            data = recvall(conn, 12)
            if not data: break
            
            fx, fy, fz = struct.unpack('<fff', data)
            current_force_vec = cp.array([fx, fy, fz], dtype=cp.float32)
            
            if cp.linalg.norm(current_force_vec) > 0.001:
                # FISICA ATTIVA
                f_ext[:] = 0 
                # Applichiamo la forza sui nodi centrali
                f_ext[force_mask] = current_force_vec * 200.0 
                u, v = solver.update_rk4(u, v, f_ext)
                # Smorzamento per stabilità con scale grandi
                v *= 0.98 
            else:
                # FREEZE: Fermiamo il movimento ma manteniamo u
                v[:] = 0.0 
                # u resta invariato: il muscolo NON torna indietro

            # SKINNING E INVIO
            u_neighbors = u[skin_idx_gpu]
            weighted_u = u_neighbors * skin_w_gpu[:, :, None]
            disp = cp.sum(weighted_u, axis=1)
            
            # new_verts sono in coordinate WORLD (es. 100, 250, 100)
            new_verts = visual_mesh_vertex_gpu + disp
            
            verts_cpu = cp.asnumpy(new_verts)
            verts_bytes = verts_cpu.tobytes()
            header = struct.pack('<I', len(verts_bytes))
            conn.sendall(header + verts_bytes)
            
    except Exception as e:
        print(f"Errore: {e}")

if __name__ == "__main__":
    start_server()