import matplotlib.pyplot as plt
import pandas as pd
import numpy as np

file_rtt=np.loadtxt("RTT_Log_rk4.txt",skiprows=1)
plt.figure(figsize=(10, 5))


media = np.mean(file_rtt)
print(f"mean of RTT ms {media:2f}")
plt.plot(file_rtt/np.max(file_rtt),label='RTT (ms)', color='blue', linewidth=1)
plt.title('RTT Client-Server', fontsize=14)
#plt.axhline(y=media, color='red', linestyle='--', label=f'Media: {media:.2f} ms')
plt.xlabel('Frames', fontsize=12)
plt.ylabel('Time (ms)', fontsize=12)
plt.grid(True, linestyle=':', alpha=0.7)
plt.legend()


plt.savefig('plot_RTT_completed_rk4.png', dpi=300)
plt.show()

