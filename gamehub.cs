using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace SnakeGame
{
    // ==================== CLASS ĐỒNG BỘ GAME STATE ====================
    public class Wall
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double W { get; set; }
        public double H { get; set; }
    }

    public class GameState
    {
        public List<Wall> Walls { get; set; } = new List<Wall>();
        public List<Food> Foods { get; set; } = new List<Food>();
        public string MapSeed { get; set; } = string.Empty;
        public bool IsGameRunning { get; set; } = false;
        public string Difficulty { get; set; } = "medium";
        public int WorldSize { get; set; } = 6000;
    }

    public class PlayerInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public double Score { get; set; }
        public string BodyJson { get; set; } = string.Empty;
        public string ConnectionId { get; set; } = string.Empty;
        public bool IsAlive { get; set; } = true;
    }

    // ==================== GAME HUB CHÍNH ====================
    public class GameHub : Hub
    {
        // Lưu danh sách phòng: roomCode -> (playerId -> PlayerInfo)
        public static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, PlayerInfo>> Rooms
            = new ConcurrentDictionary<string, ConcurrentDictionary<string, PlayerInfo>>();

        // Lưu game state cho mỗi phòng: roomCode -> GameState
        public static readonly ConcurrentDictionary<string, GameState> GameStates
            = new ConcurrentDictionary<string, GameState>();

        // ==================== TẠO PHÒNG ====================
        public async Task CreateRoom(string roomCode, string playerId, string playerName, string difficulty)
        {
            Console.WriteLine($"[CreateRoom] roomCode={roomCode}, playerId={playerId}, playerName={playerName}, difficulty={difficulty}");

            if (!Rooms.ContainsKey(roomCode))
            {
                // Tạo phòng mới
                var newRoom = new ConcurrentDictionary<string, PlayerInfo>();
                var player = new PlayerInfo
                {
                    Id = playerId,
                    Name = playerName,
                    ConnectionId = Context.ConnectionId,
                    IsAlive = true
                };
                newRoom[playerId] = player;
                Rooms[roomCode] = newRoom;

                // Tạo game state mới cho phòng
                var gameState = new GameState
                {
                    Difficulty = difficulty,
                    MapSeed = DateTime.Now.Ticks.ToString(),
                    IsGameRunning = false
                };
                
                // Sinh tường dựa trên độ khó và seed
                gameState.Walls = GenerateWalls(difficulty, gameState.MapSeed, gameState.WorldSize);
                
                // Sinh thức ăn ban đầu
                gameState.Foods = GenerateInitialFoods(100, gameState.WorldSize);
                
                GameStates[roomCode] = gameState;

                await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);
                
                // Gửi danh sách người chơi và game state cho người tạo
                await Clients.Caller.SendAsync("RoomPlayers", newRoom.Values);
                await Clients.Caller.SendAsync("GameState", gameState);
                
                Console.WriteLine($"✅ Room {roomCode} created successfully.");
            }
            else
            {
                await Clients.Caller.SendAsync("Error", "Phòng đã tồn tại");
            }
        }

        // ==================== VÀO PHÒNG ====================
        public async Task JoinRoom(string roomCode, string playerId, string playerName)
        {
            Console.WriteLine($"[JoinRoom] roomCode={roomCode}, playerId={playerId}, playerName={playerName}");

            if (Rooms.TryGetValue(roomCode, out var players))
            {
                // Thêm hoặc cập nhật người chơi
                if (!players.ContainsKey(playerId))
                {
                    var player = new PlayerInfo
                    {
                        Id = playerId,
                        Name = playerName,
                        ConnectionId = Context.ConnectionId,
                        IsAlive = true
                    };
                    players[playerId] = player;
                }
                else
                {
                    players[playerId].ConnectionId = Context.ConnectionId;
                    players[playerId].IsAlive = true;
                }

                await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);
                
                // Gửi danh sách người chơi cho tất cả
                await Clients.Group(roomCode).SendAsync("RoomPlayers", players.Values);
                
                // Gửi game state cho người mới vào
                if (GameStates.TryGetValue(roomCode, out var gameState))
                {
                    await Clients.Caller.SendAsync("GameState", gameState);
                }
                
                Console.WriteLine($"✅ Player {playerId} joined room {roomCode}. Total players: {players.Count}");
            }
            else
            {
                await Clients.Caller.SendAsync("Error", "Phòng không tồn tại");
            }
        }

        // ==================== BẮT ĐẦU GAME ====================
        public async Task StartGameInRoom(string roomCode)
        {
            if (GameStates.TryGetValue(roomCode, out var gameState))
            {
                gameState.IsGameRunning = true;
                await Clients.Group(roomCode).SendAsync("GameStarted", gameState);
                Console.WriteLine($"🎮 Game started in room {roomCode}");
            }
        }

        // ==================== CẬP NHẬT NGƯỜI CHƠI ====================
        public async Task UpdatePlayer(string roomCode, string playerId, PlayerInfo updatedInfo)
        {
            if (Rooms.TryGetValue(roomCode, out var players) && players.ContainsKey(playerId))
            {
                var player = players[playerId];
                player.X = updatedInfo.X;
                player.Y = updatedInfo.Y;
                player.Score = updatedInfo.Score;
                player.BodyJson = updatedInfo.BodyJson;
                player.IsAlive = updatedInfo.IsAlive;

                // Broadcast cho tất cả người chơi trong phòng
                await Clients.Group(roomCode).SendAsync("RoomPlayers", players.Values);
            }
        }

        // ==================== THÊM THỨC ĂN (KHI NGƯỜI CHẾT) ====================
        public async Task AddFoodToGame(string roomCode, Food food)
        {
            if (GameStates.TryGetValue(roomCode, out var gameState))
            {
                gameState.Foods.Add(food);
                await Clients.Group(roomCode).SendAsync("FoodAdded", food);
            }
        }

        // ==================== XÓA THỨC ĂN (KHI ĂN) ====================
        public async Task RemoveFood(string roomCode, string foodId)
        {
            if (GameStates.TryGetValue(roomCode, out var gameState))
            {
                var food = gameState.Foods.FirstOrDefault(f => f.Id == foodId);
                if (food != null)
                {
                    gameState.Foods.Remove(food);
                    await Clients.Group(roomCode).SendAsync("FoodRemoved", foodId);
                }
            }
        }

        // ==================== RỜI PHÒNG ====================
        public async Task LeaveRoom(string roomCode, string playerId)
        {
            if (Rooms.TryGetValue(roomCode, out var players))
            {
                if (players.TryRemove(playerId, out _))
                {
                    Console.WriteLine($"👋 Player {playerId} left room {roomCode}");
                    
                    if (players.IsEmpty)
                    {
                        // Xóa phòng nếu không còn ai
                        Rooms.TryRemove(roomCode, out _);
                        GameStates.TryRemove(roomCode, out _);
                        Console.WriteLine($"🗑️ Room {roomCode} deleted (empty)");
                    }
                    else
                    {
                        await Clients.Group(roomCode).SendAsync("RoomPlayers", players.Values);
                    }
                    
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomCode);
                }
            }
        }

        // ==================== SINH TƯỜNG (ĐỒNG BỘ QUA SEED) ====================
        private List<Wall> GenerateWalls(string difficulty, string seed, int worldSize)
        {
            var walls = new List<Wall>();
            var random = new Random(seed.GetHashCode());
            
            int count = difficulty == "easy" ? 15 : difficulty == "medium" ? 40 : 80;

            for (int i = 0; i < count; i++)
            {
                double type = random.NextDouble();
                double w, h;

                if (type < 0.33)
                {
                    w = 300 + random.NextDouble() * 400;
                    h = 60 + random.NextDouble() * 80;
                }
                else if (type < 0.66)
                {
                    w = 60 + random.NextDouble() * 80;
                    h = 300 + random.NextDouble() * 400;
                }
                else
                {
                    w = 120 + random.NextDouble() * 200;
                    h = 120 + random.NextDouble() * 200;
                }

                walls.Add(new Wall
                {
                    X = random.NextDouble() * (worldSize - w),
                    Y = random.NextDouble() * (worldSize - h),
                    W = w,
                    H = h
                });
            }

            return walls;
        }

        // ==================== SINH THỨC ĂN BAN ĐẦU ====================
        private List<Food> GenerateInitialFoods(int count, int worldSize)
        {
            var foods = new List<Food>();
            var random = new Random();

            for (int i = 0; i < count; i++)
            {
                double rand = random.NextDouble();
                string type, color;
                double size;
                string? icon = null;

                if (rand < 0.55) 
                { 
                    type = "normal"; size = 6; color = "gold"; 
                }
                else if (rand < 0.70) 
                { 
                    type = "mega"; size = 14; color = "violet"; icon = "💎"; 
                }
                else if (rand < 0.80) 
                { 
                    type = "magnet"; size = 12; color = "cyan"; icon = "🧲"; 
                }
                else if (rand < 0.88) 
                { 
                    type = "shield"; size = 12; color = "deepskyblue"; icon = "🛡"; 
                }
                else if (rand < 0.95) 
                { 
                    type = "ghost"; size = 12; color = "purple"; icon = "👻"; 
                }
                else 
                { 
                    type = "lucky"; size = 13; color = "lime"; icon = "🍀"; 
                }

                var food = new Food(random.NextDouble() * worldSize, random.NextDouble() * worldSize)
                {
                    Type = type,
                    Size = size,
                    Color = color,
                    Icon = icon
                };
                foods.Add(food);
            }

            return foods;
        }

        // ==================== NGẮT KẾT NỐI ====================
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Console.WriteLine($"❌ Client disconnected: {Context.ConnectionId}");

            foreach (var room in Rooms)
            {
                var playerEntry = room.Value.FirstOrDefault(x => x.Value.ConnectionId == Context.ConnectionId);
                if (playerEntry.Key != null)
                {
                    if (room.Value.TryRemove(playerEntry.Key, out _))
                    {
                        Console.WriteLine($"👋 Player {playerEntry.Key} removed from room {room.Key}");
                        
                        if (room.Value.IsEmpty)
                        {
                            Rooms.TryRemove(room.Key, out _);
                            GameStates.TryRemove(room.Key, out _);
                            Console.WriteLine($"🗑️ Room {room.Key} deleted (empty)");
                        }
                        else
                        {
                            await Clients.Group(room.Key).SendAsync("RoomPlayers", room.Value.Values);
                        }
                    }
                    break;
                }
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}