{
    string configFilePath = "multiplier.cfg";
    if (args.Length == 1)
    {
        configFilePath = args[0];
    }

    string? strDigitsOperand1;
    string? strDigitsOperand2;
    string? strAnswersPerMin;
    string? strConsecutiveCorrect;
    string? loggerFilePath;
    string? strNonRepeatQueueLength;
    string? strReinforceRepeatCap;
    {
        using var sr = new StreamReader(configFilePath);

        strDigitsOperand1 = sr.ReadLine();
        strDigitsOperand2 = sr.ReadLine();
        strAnswersPerMin = sr.ReadLine();
        strConsecutiveCorrect = sr.ReadLine();

        loggerFilePath = sr.ReadLine();
        strNonRepeatQueueLength = sr.ReadLine();
        strReinforceRepeatCap = sr.ReadLine();
    }

    var digitsOperand1 = int.Parse(strDigitsOperand1!);
    var digitsOperand2 = int.Parse(strDigitsOperand2!);
    var answersPerMin = double.Parse(strAnswersPerMin!);
    var consecutiveCorrect = int.Parse(strConsecutiveCorrect!);

    int nonRepeatQueueLength = 5;
    if (strNonRepeatQueueLength is not null)
    {
        nonRepeatQueueLength = int.Parse(strNonRepeatQueueLength!);
    }
    int reinforceRepeatCap = 5;
    if (strReinforceRepeatCap is not null)
    {
        reinforceRepeatCap = int.Parse(strReinforceRepeatCap!);
    }

    var logging = loggerFilePath is not null && !string.IsNullOrWhiteSpace(loggerFilePath);

    var game = new Game(digitsOperand1, digitsOperand2, answersPerMin, consecutiveCorrect, nonRepeatQueueLength, reinforceRepeatCap);
    game.Start(logging);
    if (logging)
    {
        using var sw = new StreamWriter(loggerFilePath!);
        foreach (var answer in game.LoggedAnswers)
        {
            sw.WriteLine(answer);
        }
        Console.WriteLine($"Answers logged in file '{loggerFilePath}'.");
    }
    Console.Write("Press any key to exit...");
    Console.ReadKey();
}

internal class Answer((int, int) operands, int answer, TimeSpan timeSpent)
{
    public (int, int) Operands { get; } = operands;
    public int AnswerValue { get; } = answer;
    public bool IsCorrect => Operands.Item1 * Operands.Item2 == AnswerValue;
    public TimeSpan TimeSpent { get; } = timeSpent;
    public override string ToString()
    {
        var question = $"{Operands.Item1}×{Operands.Item2}";
        var sign = IsCorrect ? "=" : "≠";
        return $"{Operands.Item1}×{Operands.Item2}{sign}{AnswerValue},{TimeSpent.TotalSeconds:F2}";
    }
}

internal class Game(int digitsOperand1, int digitsOperand2, double answersPerMin, int consecutiveCorrect, int nonRepeatQueueLength, int reinforceRepeatCap)
{
    private readonly Dictionary<(int,int), int> _pastErrors = [];
    private readonly Dictionary<(int, int), int> _pastSlowAnswers = new();

    public int DigitsOperand1 { get; } = digitsOperand1;

    public int DigitsOperand2 { get; } = digitsOperand2;

    public double AnswersPerMinRequired { get; } = answersPerMin;

    public int ConsecutiveCorrectRequired { get; } = consecutiveCorrect;

    public int NonRepeatQueueLength { get; } = nonRepeatQueueLength;

    public int ReinforceRepeatCap { get; } = reinforceRepeatCap;

    public readonly List<Answer> LoggedAnswers = [];

    public void Start(bool logging)
    {
        Console.WriteLine($"Quiz of multiplying {DigitsOperand1}-digit and {DigitsOperand2}-digit numbers.");
        Console.WriteLine();
        Console.WriteLine($"Logging: {(logging?"Enabled":"Disabled")}.");
        Console.WriteLine($"Required consecutive correctness: {ConsecutiveCorrectRequired}.");
        Console.WriteLine($"Required speed: {AnswersPerMinRequired} Answers/min.");
        Console.WriteLine($"Non-repeat length: {NonRepeatQueueLength}.");
        Console.WriteLine($"Reinforcement repeats capped at: {ReinforceRepeatCap}.");
        Console.WriteLine();
        Console.Write("Press any key when ready...");
        Console.ReadKey();
        Console.Clear();

        LoggedAnswers.Clear();

        var rand = new Random();
        var consecutiveCorrectAnswersSecondsUsed = new List<double>();
        var correctPostClearing = 0;
        Queue<(int, int)> lastOperands = [];
        bool succeeded = false;
        while (!succeeded)
        {
        __regenerate:
            var (operand1, operand2) = GenerateOperands(rand);
            var orderedOperands = OrderOperands((operand1, operand2));
            if (lastOperands.Contains(orderedOperands))
            {
                goto __regenerate;
            }
            lastOperands.Enqueue(orderedOperands);
            if (lastOperands.Count > NonRepeatQueueLength)
            {
                lastOperands.Dequeue();
            }

            var answer = operand1 * operand2;
            Console.Write($"{operand1} × {operand2} = ");
            var startTime = DateTime.Now;
            var userAnswer = Console.ReadLine();
            var elapsedTime = DateTime.Now - startTime;
            double? actualAnswersPerMin = null;

            if (int.TryParse(userAnswer, out var parsedAnswer) && parsedAnswer == answer)
            {
                if (_pastErrors.Count == 0)
                {
                    correctPostClearing++;
                }

                if (_pastErrors.TryGetValue(orderedOperands, out var errorCount))
                {
                    _pastErrors[orderedOperands] = Math.Max(0, errorCount - 1);
                    if (_pastErrors[orderedOperands] == 0) _pastErrors.Remove(orderedOperands);
                }

                actualAnswersPerMin = null;
                consecutiveCorrectAnswersSecondsUsed.Add(elapsedTime.TotalSeconds);

                if (_pastErrors.Count == 0)
                {
                    if (consecutiveCorrectAnswersSecondsUsed.Count > ConsecutiveCorrectRequired)
                        consecutiveCorrectAnswersSecondsUsed.RemoveRange(0,
                            consecutiveCorrectAnswersSecondsUsed.Count - ConsecutiveCorrectRequired);

                    var totalTime = consecutiveCorrectAnswersSecondsUsed.Sum();
                    actualAnswersPerMin = consecutiveCorrectAnswersSecondsUsed.Count / (totalTime / 60.0);

                    if (correctPostClearing >= ConsecutiveCorrectRequired && actualAnswersPerMin >= AnswersPerMinRequired)
                    {
                        succeeded = true;
                    }
                }

                string suffix;
                if (_pastErrors.Count > 0)
                {
                    suffix = $"({PrintRemainingPastErrorAndRepeatInstanceNumbers()})";
                }
                else
                {
                    suffix = $"(CC={correctPostClearing}/{ConsecutiveCorrectRequired}";
                    if (actualAnswersPerMin.HasValue)
                    {
                        suffix += $", A/min={actualAnswersPerMin.Value:F2}/{AnswersPerMinRequired:F2}";
                    }
                    suffix += ")";
                }

                Console.WriteLine($"Correct! {suffix}");
            }
            else
            {
                correctPostClearing = 0;
                consecutiveCorrectAnswersSecondsUsed.Clear();
                var newErrors = Math.Min(_pastErrors.GetValueOrDefault(orderedOperands, 0) + 1, ReinforceRepeatCap);
                _pastErrors[orderedOperands] = newErrors;
                Console.WriteLine(
                    $"Incorrect! ({PrintRemainingPastErrorAndRepeatInstanceNumbers()})");
            }

            if (logging)
            {
                var answerEntry = new Answer((operand1, operand2), parsedAnswer, elapsedTime);
                LoggedAnswers.Add(answerEntry);
            }

            Console.Write("Press any key to continue...");
            Console.ReadKey();
            Console.Clear();

            if (succeeded)
            {
                Console.Clear();
                Console.WriteLine(
                    $"Congratulations! You made {consecutiveCorrectAnswersSecondsUsed.Count} consecutive correct answer(s) at {actualAnswersPerMin:F2} A/min satisfying the required {AnswersPerMinRequired:F2} A/min.");
            }
        }
    }
    
    private string PrintRemainingPastErrorAndRepeatInstanceNumbers()
    {
        var totalFixes = _pastErrors.Values.Sum();
        return $"{totalFixes} blocking question(s) remain for {_pastErrors.Count} incorrect sets.";
    }

    private static (int, int) OrderOperands((int operand1, int operand2) operands)
    {
        return operands.operand1 < operands.operand2 ? operands : (operands.operand2, operands.operand1);
    }

    private (int operand1, int operand2) GenerateOperands(Random rand)
    {
        var flip = rand.Next(0, 2);

        if (_pastErrors.Count > 0)
        {
            var pickErrors = rand.Next(0, 2);
            if (pickErrors == 1)
            {
                // Pick a random error from past errors
                var error = _pastErrors.ElementAt(rand.Next(_pastErrors.Count));
                var errorOperands = OrderOperands(error.Key);
                return flip == 0 ? errorOperands : (errorOperands.Item2, errorOperands.Item1);
            }
        }

        if (flip == 0)
        {
            var operand1 = rand.Next(2, (int)Math.Pow(10, DigitsOperand1));
            var operand2 = rand.Next(2, (int)Math.Pow(10, DigitsOperand2));
            return (operand1, operand2);
        }
        else
        {
            var operand1 = rand.Next(2, (int)Math.Pow(10, DigitsOperand2));
            var operand2 = rand.Next(2, (int)Math.Pow(10, DigitsOperand1));
            return (operand1, operand2);
        }
    }
}