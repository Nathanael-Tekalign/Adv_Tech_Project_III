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

            // His contribution — notify connected clients a player joined
            await _hubContext.Clients.Group(session.RoomCode)
                .SendAsync("RefreshPlayers");

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

            if (!session.Quiz.Questions.Any())
            {
                TempData["Error"] = "This quiz has no questions.";
                return RedirectToAction(nameof(AddPlayers), new { sessionId });
            }

            // His contribution — store question order in database instead of session
            var questionIds = session.Quiz.Questions
                .OrderBy(q => Guid.NewGuid())
                .Select(q => q.Id)
                .ToList();

            session.State = GameState.InProgress;
            session.CurrentQuestionIndex = 0;
            session.QuestionOrderJson = JsonSerializer.Serialize(questionIds);

            await _context.SaveChangesAsync();

            // His contribution — notify connected clients game started
            await _hubContext.Clients.Group(session.RoomCode)
                .SendAsync("GameStarted", session.Id);

            HttpContext.Session.SetInt32("SessionId", session.Id);
            HttpContext.Session.SetInt32("CurrentPlayerIndex", 0);

            return RedirectToAction(nameof(Play), new { sessionId });
        }

        [HttpGet]
        public async Task<IActionResult> Play(int sessionId)
        {
            var session = await _context.GameSessions
                .Include(gs => gs.Players)
                .Include(gs => gs.Quiz)
                .FirstOrDefaultAsync(gs => gs.Id == sessionId);

            if (session == null)
            {
                return NotFound();
            }

            var players = session.Players.OrderBy(p => p.Id).ToList();
            int currentPlayerIndex = HttpContext.Session.GetInt32("CurrentPlayerIndex") ?? 0;

            if (currentPlayerIndex >= players.Count)
            {
                return RedirectToAction(nameof(Leaderboard), new { sessionId });
            }

            var currentPlayer = players[currentPlayerIndex];

            // His contribution — question order stored in DB not session
            var questionOrder = JsonSerializer.Deserialize<List<int>>(
                session.QuestionOrderJson ?? "[]") ?? new List<int>();

            if (!questionOrder.Any())
            {
                TempData["Error"] = "Question order was not found. Please start the game again.";
                return RedirectToAction(nameof(AddPlayers), new { sessionId });
            }

            if (session.CurrentQuestionIndex >= questionOrder.Count)
            {
                currentPlayer.IsFinished = true;
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(NextPlayer), new { sessionId });
            }

            int questionId = questionOrder[session.CurrentQuestionIndex];
            var question = await _context.Questions.FirstOrDefaultAsync(q => q.Id == questionId);

            if (question == null)
            {
                return NotFound();
            }

            ViewBag.SessionId = sessionId;
            ViewBag.PlayerName = currentPlayer.Name;
            ViewBag.QuestionNumber = session.CurrentQuestionIndex + 1;
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
            var session = await _context.GameSessions
                .Include(gs => gs.Players)
                .FirstOrDefaultAsync(gs => gs.Id == sessionId);

            if (session == null)
            {
                return NotFound();
            }

            var players = session.Players.OrderBy(p => p.Id).ToList();
            int currentPlayerIndex = HttpContext.Session.GetInt32("CurrentPlayerIndex") ?? 0;

            if (currentPlayerIndex >= players.Count)
            {
                return RedirectToAction(nameof(Leaderboard), new { sessionId });
            }

            var currentPlayer = players[currentPlayerIndex];

            var question = await _context.Questions.FirstOrDefaultAsync(q => q.Id == questionId);
            if (question == null)
            {
                return NotFound();
            }

            // His contribution — prevent double submissions
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
            session.CurrentQuestionIndex += 1;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Play), new { sessionId });
        }

        [HttpGet]
        public async Task<IActionResult> NextPlayer(int sessionId)
        {
            var session = await _context.GameSessions
                .Include(gs => gs.Players)
                .FirstOrDefaultAsync(gs => gs.Id == sessionId);

            if (session == null)
            {
                return NotFound();
            }

            var players = session.Players.OrderBy(p => p.Id).ToList();
            int currentPlayerIndex = HttpContext.Session.GetInt32("CurrentPlayerIndex") ?? 0;

            ViewBag.SessionId = sessionId;
            ViewBag.CurrentPlayerName = currentPlayerIndex < players.Count
                ? players[currentPlayerIndex].Name
                : null;

            ViewBag.NextPlayerName = currentPlayerIndex + 1 < players.Count
                ? players[currentPlayerIndex + 1].Name
                : null;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmNextPlayer(int sessionId)
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

            int currentPlayerIndex = HttpContext.Session.GetInt32("CurrentPlayerIndex") ?? 0;
            currentPlayerIndex++;

            if (currentPlayerIndex >= session.Players.Count)
            {
                session.State = GameState.Finished;
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Leaderboard), new { sessionId });
            }

            HttpContext.Session.SetInt32("CurrentPlayerIndex", currentPlayerIndex);

            // Randomize question order fresh for each new player
            var newQuestionOrder = session.Quiz.Questions
                .OrderBy(q => Guid.NewGuid())
                .Select(q => q.Id)
                .ToList();

            session.CurrentQuestionIndex = 0;
            session.QuestionOrderJson = JsonSerializer.Serialize(newQuestionOrder);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Play), new { sessionId });
        }

        [HttpGet]
        public async Task<IActionResult> Leaderboard(int sessionId)
        {
            var session = await _context.GameSessions
                .Include(gs => gs.Players)
                .ThenInclude(p => p.PlayerAnswers)
                .FirstOrDefaultAsync(gs => gs.Id == sessionId);

            if (session == null)
            {
                return NotFound();
            }

            var leaderboard = session.Players
                .OrderByDescending(p => p.TotalScore)
                .ThenBy(p => p.PlayerAnswers.Any()
                    ? p.PlayerAnswers.Average(a => a.TimeTakenSeconds)
                    : double.MaxValue)
                .ToList();

            ViewBag.SessionId = sessionId;
            ViewBag.RoomCode = session.RoomCode;

            return View(leaderboard);
        }
    }
}