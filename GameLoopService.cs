using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SnakeGame
{
    public class GameLoopService : BackgroundService
    {
        private readonly ILogger<GameLoopService> _logger;
        private static readonly ConcurrentDictionary<string, DateTime> RoomLastActive 
            = new ConcurrentDictionary<string, DateTime>();

        public GameLoopService(ILogger<GameLoopService> logger)
        {
            _logger = logger;
        }

        public static void UpdateActivity(string roomCode)
        {
            RoomLastActive[roomCode] = DateTime.UtcNow;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("GameLoopService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Xóa các phòng không hoạt động quá 10 phút
                    var expiredRooms = RoomLastActive
                        .Where(kvp => (DateTime.UtcNow - kvp.Value).TotalMinutes > 10)
                        .Select(kvp => kvp.Key)
                        .ToList();

                    foreach (var room in expiredRooms)
                    {
                        if (RoomLastActive.TryRemove(room, out _))
                        {
                            // Xóa dữ liệu phòng trong GameHub nếu có
                            // Cần truy cập vào Rooms của GameHub -> khó vì static
                            _logger.LogInformation($"Removed inactive room: {room}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in GameLoopService");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}