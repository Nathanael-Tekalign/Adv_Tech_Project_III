using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TriviaGame.Data;
using TriviaGame.Hubs;
using TriviaGame.Models;

namespace TriviaGame.Controllers
{
    public class GameController : Controller
    {
        private readonly TriviaGameContext _context;
        private readonly IHubContext<TriviaHub> _hubContext;

        public GameController(TriviaGameContext context, IHubContext<TriviaHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        [HttpGet]
        public async Task<IActionResult> Start()
        {
            var quizzes = await _context.Quizzes
                .Where(q => q.IsPublished)
                .OrderBy(q => q.Title)
                .ToListAsync();

            return View(quizzes);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSession(int quizId, string? roomCode)
        {
            var quiz = await _context.Quizzes
                .Include(q => q.Questions)
                .FirstOrDefaultAsync(q => q.Id == quizId);

            if (quiz == null)
            {
                TempData["Error"] = "Quiz not found.";
                return RedirectToAction(nameof(Start));
            }

            if (quiz.Questions == null || !quiz.Questions.Any())
            {
                TempData["Error"] = "This quiz has no questions yet.";
                return RedirectToAction(nameof(Start));
            }

            var session = new GameSession
            {
                QuizId = quizId,
                RoomCode = string.IsNullOrWhiteSpace(roomCode)
                    ? Guid.NewGuid().ToString("N")[..6].ToUpper()
                    : roomCode.Trim().ToUpper(),
                State = GameState.Lobby,
                CurrentQuestionIndex = 0,
                CreatedAt = DateTime.Now,
                QuestionOrderJson = null
            };

            _context.GameSessions.Add(session);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(AddPlayers), new { sessionId = session.Id });
        }

        [HttpGet]
        public async Task<IActionResult> AddPlayers(int sessionId)
        {
            var session = await _context.GameSessions
                .Include(gs => gs.Quiz)
                .Include(gs => gs.Players)
                .FirstOrDefaultAsync(gs => gs.Id == sessionId);

            if (session == null)
            {
                return NotFound();
            }

            return View(session);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPlayer(int sessionId, string playerName)
        {
            var session = await _context.GameSessions
                .Include(gs => gs.Players)
                .FirstOrDefaultAsync(gs => gs.Id == sessionId);

            if (session == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(playerName))
            {
                TempData["Error"] = "Player name is required.";
                return RedirectToAction(nameof(AddPlayers), new { sessionId });
            }

            var trimmedName = playerName.Trim();

            bool alreadyExists = session.Players.Any(p =>
                p.Name.ToLower() == trimmedName.ToLower());

            if (alreadyExists)
            {
                TempData["Error"] = "That player name already exists.";
                return RedirectToAction(nameof(AddPlayers), new { sessionId });
            }

            var player = new Player
            {
                GameSessionId = sessionId,
                Name = trimmedName,
                ConnectionId = string.Empty,
                IsFinished = false,
                TotalScore = 0
            };

            _context.Players.Add(player);
            await _context.SaveChangesAsync();

            await _hubContext.Clients.Group(session.RoomCode).SendAsync("RefreshPlayers");

            return RedirectToAction(nameof(AddPlayers), new { sessionId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BeginGame(int sessionId)
        {
            var session = await _context.GameSessions
                .Include(gs => gs.Quiz)
                .ThenInclude(q => q.Questions)
                .Include(gs => gs.Players)
                .FirstOrDefaultAsync(gs => gs.Id == sessionId);

            if (session == null)
            {
                return NotFound();
            }

            if (!session.Players.Any())
            {
                TempData["Error"] = "Add at least one player before starting.";
                return RedirectToAction(nameof(AddPlayers), new { sessionId });
            }

            var questionIds = session.Quiz.Questions
                .OrderBy(q => Guid.NewGuid())
                .Select(q => q.Id)
                .ToList();

            if (!questionIds.Any())
            {
                TempData["Error"] = "This quiz has no questions.";
                return RedirectToAction(nameof(AddPlayers), new { sessionId });
            }

            session.State = GameState.InProgress;
            session.CurrentQuestionIndex = 0;
            session.QuestionOrderJson = JsonSerializer.Serialize(questionIds);

            await _context.SaveChangesAsync();

            await _hubContext.Clients.Group(session.RoomCode)
                .SendAsync("GameStarted", session.Id);

            HttpContext.Session.SetInt32("SessionId", session.Id);

           
            return RedirectToAction(nameof(Join), new { roomCode = session.RoomCode });
        }

        [HttpGet]
        public IActionResult Join(string? roomCode = null)
        {
            ViewBag.RoomCode = roomCode ?? string.Empty;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Join(string roomCode, string playerName)
        {
            if (string.IsNullOrWhiteSpace(roomCode) || string.IsNullOrWhiteSpace(playerName))
            {
                TempData["Error"] = "Room code and player name are required.";
                return RedirectToAction(nameof(Join));
            }

            string normalizedRoomCode = roomCode.Trim().ToUpper();
            string trimmedPlayerName = playerName.Trim();

            var session = await _context.GameSessions
                .Include(gs => gs.Players)
                .FirstOrDefaultAsync(gs => gs.RoomCode == normalizedRoomCode);

            if (session == null)
            {
                TempData["Error"] = "Room not found.";
                return RedirectToAction(nameof(Join));
            }

            if (session.State == GameState.Finished)
            {
                TempData["Error"] = "This game has already finished.";
                return RedirectToAction(nameof(Join));
            }

            var player = session.Players
                .FirstOrDefault(p => p.Name.ToLower() == trimmedPlayerName.ToLower());

            if (player == null)
            {
                player = new Player
                {
                    GameSessionId = session.Id,
                    Name = trimmedPlayerName,
                    ConnectionId = string.Empty,
                    TotalScore = 0,
                    IsFinished = false
                };

                _context.Players.Add(player);
                await _context.SaveChangesAsync();

                await _hubContext.Clients.Group(session.RoomCode).SendAsync("RefreshPlayers");
            }

            HttpContext.Session.SetInt32("PlayerId", player.Id);
            HttpContext.Session.SetInt32("SessionId", session.Id);

            if (session.State == GameState.Lobby)
            {
                return RedirectToAction(nameof(AddPlayers), new { sessionId = session.Id });
            }

            return RedirectToAction(nameof(Play), new { sessionId = session.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Play(int sessionId)
        {
            int? playerId = HttpContext.Session.GetInt32("PlayerId");
            if (playerId == null)
            {
                return RedirectToAction(nameof(Join));
            }

            var session = await _context.GameSessions
                .Include(gs => gs.Players)
                .Include(gs => gs.Quiz)
                .FirstOrDefaultAsync(gs => gs.Id == sessionId);

            if (session == null)
            {
                return NotFound();
            }

            var currentPlayer = session.Players.FirstOrDefault(p => p.Id == playerId.Value);
            if (currentPlayer == null)
            {
                return RedirectToAction(nameof(Join), new { roomCode = session.RoomCode });
            }

            var questionOrder = JsonSerializer.Deserialize<List<int>>(session.QuestionOrderJson ?? "[]")
                               ?? new List<int>();

            if (!questionOrder.Any())
            {
                TempData["Error"] = "Question order was not found. Please start the game again.";
                return RedirectToAction(nameof(AddPlayers), new { sessionId });
            }

            int currentQuestionIndex = await _context.PlayerAnswers
                .CountAsync(pa => pa.PlayerId == currentPlayer.Id);

            if (currentQuestionIndex >= questionOrder.Count)
            {
                if (!currentPlayer.IsFinished)
                {
                    currentPlayer.IsFinished = true;
                    await _context.SaveChangesAsync();
                }

                bool allFinished = await _context.Players
                    .Where(p => p.GameSessionId == sessionId)
                    .AllAsync(p => p.IsFinished);

                if (allFinished && session.State != GameState.Finished)
                {
                    session.State = GameState.Finished;
                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(nameof(Leaderboard), new { sessionId });
            }

            int questionId = questionOrder[currentQuestionIndex];

            var question = await _context.Questions.FirstOrDefaultAsync(q => q.Id == questionId);
            if (question == null)
            {
                return NotFound();
            }

            ViewBag.SessionId = sessionId;
            ViewBag.PlayerId = currentPlayer.Id;
            ViewBag.PlayerName = currentPlayer.Name;
            ViewBag.QuestionNumber = currentQuestionIndex + 1;
            ViewBag.TotalQuestions = questionOrder.Count;

            return View(question);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitAnswer(
            int sessionId,
            int questionId,
            string selectedAnswer,
            double timeTakenSeconds)
        {
            int? playerId = HttpContext.Session.GetInt32("PlayerId");
            if (playerId == null)
            {
                return RedirectToAction(nameof(Join));
            }

            var session = await _context.GameSessions
                .Include(gs => gs.Players)
                .FirstOrDefaultAsync(gs => gs.Id == sessionId);

            if (session == null)
            {
                return NotFound();
            }

            var currentPlayer = session.Players.FirstOrDefault(p => p.Id == playerId.Value);
            if (currentPlayer == null)
            {
                return RedirectToAction(nameof(Join), new { roomCode = session.RoomCode });
            }

            var question = await _context.Questions.FirstOrDefaultAsync(q => q.Id == questionId);
            if (question == null)
            {
                return NotFound();
            }

            bool alreadyAnswered = await _context.PlayerAnswers
                .AnyAsync(pa => pa.PlayerId == currentPlayer.Id && pa.QuestionId == question.Id);

            if (alreadyAnswered)
            {
                return RedirectToAction(nameof(Play), new { sessionId });
            }

            bool isCorrect = string.Equals(
                selectedAnswer?.Trim(),
                question.CorrectAnswer?.Trim(),
                StringComparison.OrdinalIgnoreCase);

            int pointsAwarded = isCorrect ? question.Points : 0;
            currentPlayer.TotalScore += pointsAwarded;

            var playerAnswer = new PlayerAnswer
            {
                PlayerId = currentPlayer.Id,
                QuestionId = question.Id,
                SelectedAnswer = selectedAnswer ?? string.Empty,
                IsCorrect = isCorrect,
                PointsAwarded = pointsAwarded,
                TimeTakenSeconds = timeTakenSeconds
            };

            _context.PlayerAnswers.Add(playerAnswer);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Play), new { sessionId });
        }

        [HttpGet]
        public async Task<IActionResult> Leaderboard(int sessionId)
        {
            var session = await _context.GameSessions
                .Include(gs => gs.Players)
                .FirstOrDefaultAsync(gs => gs.Id == sessionId);

            if (session == null)
            {
                return NotFound();
            }

            var leaderboard = session.Players
                .OrderByDescending(p => p.TotalScore)
                .ThenBy(p => p.Name)
                .ToList();

            ViewBag.SessionId = sessionId;
            ViewBag.RoomCode = session.RoomCode;

            return View(leaderboard);
        }


        [HttpGet]
        public async Task<IActionResult> NextPlayer(int sessionId)
        {
            return RedirectToAction(nameof(Leaderboard), new { sessionId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmNextPlayer(int sessionId)
        {
            return RedirectToAction(nameof(Leaderboard), new { sessionId });
        }
    }
}