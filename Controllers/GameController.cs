using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TriviaGame.Data;
using TriviaGame.Models;

namespace TriviaGame.Controllers
{
    public class GameController : Controller
    {
        private readonly TriviaGameContext _context;

        public GameController(TriviaGameContext context)
        {
            _context = context;
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
                CreatedAt = DateTime.Now
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
            await _context.SaveChangesAsync();

            HttpContext.Session.SetInt32("SessionId", session.Id);
            HttpContext.Session.SetInt32("CurrentPlayerIndex", 0);
            HttpContext.Session.SetString("QuestionOrder", JsonSerializer.Serialize(questionIds));

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

            string? questionOrderJson = HttpContext.Session.GetString("QuestionOrder");
            if (string.IsNullOrWhiteSpace(questionOrderJson))
            {
                TempData["Error"] = "Question order was not found. Please start the game again.";
                return RedirectToAction(nameof(AddPlayers), new { sessionId });
            }

            var questionOrder = JsonSerializer.Deserialize<List<int>>(questionOrderJson) ?? new List<int>();

            if (session.CurrentQuestionIndex >= questionOrder.Count)
            {
                players[currentPlayerIndex].IsFinished = true;
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
            ViewBag.PlayerName = players[currentPlayerIndex].Name;
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

            var question = await _context.Questions.FirstOrDefaultAsync(q => q.Id == questionId);
            if (question == null)
            {
                return NotFound();
            }

            var currentPlayer = players[currentPlayerIndex];

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

            session.CurrentQuestionIndex = 0;

            var newQuestionOrder = session.Quiz.Questions
                .OrderBy(q => Guid.NewGuid())
                .Select(q => q.Id)
                .ToList();

            HttpContext.Session.SetString("QuestionOrder", JsonSerializer.Serialize(newQuestionOrder));

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
    }
}