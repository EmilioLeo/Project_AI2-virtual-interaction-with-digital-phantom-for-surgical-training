import pandas as pd
import matplotlib.pyplot as plt


try:
    df = pd.read_csv('performance_log.csv')
except FileNotFoundError:
    print("Errore: Assicurati che il file 'performance_log.csv' sia nella stessa cartella.")
    exit()


plt.figure(figsize=(10, 5))
plt.plot(df['Frame'], df['ProcessingTime_ms'], label='Computing time(ms)', color='blue', linewidth=1)


media = df['ProcessingTime_ms'].mean()
plt.axhline(y=media, color='red', linestyle='--', label=f'Media: {media:.2f} ms')

plt.title('Server Processing Time', fontsize=14)
plt.xlabel('Frames', fontsize=12)
plt.ylabel('Time(ms)', fontsize=12)
plt.grid(True, linestyle=':', alpha=0.7)
plt.legend()

plt.savefig('plot_server_completed.png', dpi=300)
plt.show()