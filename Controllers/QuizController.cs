using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TriviaGame.Data;
using TriviaGame.Models;

namespace TriviaGame.Controllers
{
    public class QuizController : Controller
    {
        private readonly TriviaGameContext _context;

        public QuizController(TriviaGameContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var quizzes = await _context.Quizzes
                .Include(q => q.Questions)
                .OrderByDescending(q => q.CreatedAt)
                .ToListAsync();

            return View(quizzes);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Quiz quiz)
        {
            if (!ModelState.IsValid)
            {
                return View(quiz);
            }

            quiz.CreatedAt = DateTime.Now;
            quiz.IsPublished = false;

            _context.Quizzes.Add(quiz);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Quiz created. Now add some questions.";
            return RedirectToAction(nameof(Edit), new { id = quiz.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var quiz = await _context.Quizzes
                .Include(q => q.Questions)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (quiz == null)
            {
                return NotFound();
            }

            return View(quiz);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, string title, string description, bool isPublished)
        {
            var quiz = await _context.Quizzes.FirstOrDefaultAsync(q => q.Id == id);

            if (quiz == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                ModelState.AddModelError("Title", "Title is required.");
                return RedirectToAction(nameof(Edit), new { id });
            }

            quiz.Title = title.Trim();
            quiz.Description = description?.Trim() ?? string.Empty;
            quiz.IsPublished = isPublished;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Quiz saved.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var quiz = await _context.Quizzes
                .Include(q => q.Questions)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (quiz == null)
            {
                return NotFound();
            }

            _context.Quizzes.Remove(quiz);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Quiz deleted.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddQuestion(int quizId, string questionText,
            string answerA, string answerB, string answerC, string answerD,
            string correctAnswer, int points, int timeLimitSeconds)
        {
            var quiz = await _context.Quizzes.FirstOrDefaultAsync(q => q.Id == quizId);

            if (quiz == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(questionText) ||
                string.IsNullOrWhiteSpace(answerA) ||
                string.IsNullOrWhiteSpace(answerB) ||
                string.IsNullOrWhiteSpace(answerC) ||
                string.IsNullOrWhiteSpace(answerD) ||
                string.IsNullOrWhiteSpace(correctAnswer))
            {
                TempData["Error"] = "All fields are required.";
                return RedirectToAction(nameof(Edit), new { id = quizId });
            }

            var question = new Question
            {
                QuizId = quizId,
                QuestionText = questionText.Trim(),
                AnswerA = answerA.Trim(),
                AnswerB = answerB.Trim(),
                AnswerC = answerC.Trim(),
                AnswerD = answerD.Trim(),
                CorrectAnswer = correctAnswer.Trim().ToUpper(),
                Points = points,
                TimeLimitSeconds = timeLimitSeconds
            };

            _context.Questions.Add(question);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Question added.";
            return RedirectToAction(nameof(Edit), new { id = quizId });
        }

        [HttpGet]
        public async Task<IActionResult> EditQuestion(int id)
        {
            var question = await _context.Questions.FirstOrDefaultAsync(q => q.Id == id);

            if (question == null)
            {
                return NotFound();
            }

            return View(question);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditQuestion(int id, string questionText,
            string answerA, string answerB, string answerC, string answerD,
            string correctAnswer, int points, int timeLimitSeconds)
        {
            var question = await _context.Questions.FirstOrDefaultAsync(q => q.Id == id);

            if (question == null)
            {
                return NotFound();
            }

            question.QuestionText = questionText.Trim();
            question.AnswerA = answerA.Trim();
            question.AnswerB = answerB.Trim();
            question.AnswerC = answerC.Trim();
            question.AnswerD = answerD.Trim();
            question.CorrectAnswer = correctAnswer.Trim().ToUpper();
            question.Points = points;
            question.TimeLimitSeconds = timeLimitSeconds;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Question saved.";
            return RedirectToAction(nameof(Edit), new { id = question.QuizId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteQuestion(int id)
        {
            var question = await _context.Questions.FirstOrDefaultAsync(q => q.Id == id);

            if (question == null)
            {
                return NotFound();
            }

            int quizId = question.QuizId;

            _context.Questions.Remove(question);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Question deleted.";
            return RedirectToAction(nameof(Edit), new { id = quizId });
        }
    }
}