import pandas as pd
import matplotlib.pyplot as plt


try:
    df = pd.read_csv('performance_log_rk4.csv')
except FileNotFoundError:
    print("Errore: Assicurati che il file 'performance_log.csv' sia nella stessa cartella.")
    exit()


plt.figure(figsize=(10, 5))
plt.plot(df['Frame'], df['ProcessingTime_ms']/df['ProcessingTime_ms'].max(), label='Computing time(ms)', color='blue', linewidth=1)


media = df['ProcessingTime_ms'].mean()
print(f"mean of server computation time (ms): {media:.2f}")

plt.title('Server Processing Time', fontsize=14)
plt.xlabel('Frames', fontsize=12)
plt.ylabel('Time(ms)', fontsize=12)
plt.grid(True, linestyle=':', alpha=0.7)
plt.legend()

plt.savefig('plot_server_completed_rk4.png', dpi=300)
plt.show()

