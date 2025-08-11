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

    int nonRepeatQueueLength = 3;
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
    private readonly Dictionary<(int, int), int> _pastSlowAnswers = [];

    public int DigitsOperand1 { get; } = digitsOperand1;

    public int DigitsOperand2 { get; } = digitsOperand2;

    public double MinAnswersPerMinRequired { get; } = answersPerMin;

    public int ConsecutiveSuccessesRequired { get; } = consecutiveCorrect;

    public int NonRepeatQueueLength { get; } = nonRepeatQueueLength;

    public int ReinforceRepeatCap { get; } = reinforceRepeatCap;

    public readonly List<Answer> LoggedAnswers = [];

    public void Start(bool logging)
    {
        Console.WriteLine($"Quiz of multiplying {DigitsOperand1}-digit and {DigitsOperand2}-digit numbers.");
        Console.WriteLine();
        Console.WriteLine($"Logging: {(logging?"Enabled":"Disabled")}.");
        Console.WriteLine($"Required consecutive successes: {ConsecutiveSuccessesRequired}.");
        Console.WriteLine($"Required minimum speed: {MinAnswersPerMinRequired} Answers/min.");
        Console.WriteLine($"Non-repeat length: {NonRepeatQueueLength}.");
        Console.WriteLine($"Reinforcement repeats capped at: {ReinforceRepeatCap}.");
        Console.WriteLine();
        Console.Write("Press any key when ready...");
        Console.ReadKey();
        Console.Clear();

        LoggedAnswers.Clear();

        var rand = new Random();
 
        Queue<(int, int)> previousOperands = [];    // To avoid repeating the same operands in a row

        var maxAllowedAnswerTime = TimeSpan.FromSeconds(60.0 / MinAnswersPerMinRequired);

        int consecutiveSuccess = 0;
        TimeSpan? minTimeUsed = null;
        TimeSpan? maxTimeUsed = null;

        bool done = false;

        while (!done)
        {
        __regenerate:
            var (operand1, operand2) = GenerateOperands(rand);
            var distintOperandTuple = OrderOperands((operand1, operand2));
            if (previousOperands.Contains(distintOperandTuple))
            {
                goto __regenerate;
            }
            previousOperands.Enqueue(distintOperandTuple);
            if (previousOperands.Count > NonRepeatQueueLength)
            {
                previousOperands.Dequeue();
            }

            var answer = operand1 * operand2;
            Console.Write($"{operand1} × {operand2} = ");
            var startTime = DateTime.Now;
            var userAnswer = Console.ReadLine();

            var timeUsed = DateTime.Now - startTime;

            if (int.TryParse(userAnswer, out var parsedAnswer) && parsedAnswer == answer)
            {
                var withinTimeLimit = timeUsed <= maxAllowedAnswerTime;
                if (withinTimeLimit)
                {
                    consecutiveSuccess++;
                    minTimeUsed = minTimeUsed == null || timeUsed < minTimeUsed ? timeUsed : minTimeUsed;
                    maxTimeUsed = maxTimeUsed == null || timeUsed > maxTimeUsed ? timeUsed : maxTimeUsed;
                    _pastSlowAnswers.Remove(distintOperandTuple);
                }
                else
                {                     
                    consecutiveSuccess = 0;
                    minTimeUsed = null;
                    maxTimeUsed = null;
                }

                if (_pastErrors.Count == 0 && _pastSlowAnswers.Count == 0 && consecutiveSuccess >= ConsecutiveSuccessesRequired)
                {
                    done = true;
                }
                else
                {
                    RemoveFromDict(_pastErrors, distintOperandTuple);
                    if (!withinTimeLimit)
                    {
                        AddToDict(_pastSlowAnswers, distintOperandTuple, ReinforceRepeatCap);
                    }

                    string messageForCorrectAnswer = $"Correct taking {timeUsed.TotalSeconds:F2}s";
                    if (withinTimeLimit)
                    {
                        messageForCorrectAnswer += $" (Within time limit {maxAllowedAnswerTime.TotalSeconds:F2}s).";
                    }
                    else
                    {
                        messageForCorrectAnswer += $" (took too long for {maxAllowedAnswerTime.TotalSeconds:F2}s).";
                    }
                    if (_pastErrors.Count > 0)
                    {
                        messageForCorrectAnswer += $" ({PrintRemainingPastErrorAndRepeatInstanceNumbers()})";
                    }
                    else if (_pastSlowAnswers.Count > 0)
                    {
                        messageForCorrectAnswer += $" ({PrintRemainingPastSlowAnswers()}).";
                    }
                    else
                    {
                        messageForCorrectAnswer += $" (ConsSucc={consecutiveSuccess}/{ConsecutiveSuccessesRequired})";
                    }

                    Console.WriteLine($"{messageForCorrectAnswer}");
                }

                  
            }
            else
            {
                consecutiveSuccess = 0;
                minTimeUsed = null;
                maxTimeUsed = null;
                AddToDict(_pastErrors, distintOperandTuple, ReinforceRepeatCap);
                Console.WriteLine(
                    $"Incorrect! ({PrintRemainingPastErrorAndRepeatInstanceNumbers()})");
            }

            if (logging)
            {
                var answerEntry = new Answer((operand1, operand2), parsedAnswer, timeUsed);
                LoggedAnswers.Add(answerEntry);
            }

            if (done)
            {
                Console.Clear();
                Console.WriteLine($"Congratulations! You succeeded {consecutiveSuccess} times in a row above required {MinAnswersPerMinRequired:F2} A/min.");
                Console.WriteLine($"Max time used {maxTimeUsed!.Value.TotalSeconds:F2}s ({TimeToApm(maxTimeUsed.Value)} A/min).");
                Console.WriteLine($"Min time used {minTimeUsed!.Value.TotalSeconds:F2}s ({TimeToApm(minTimeUsed.Value)} A/min).");
            }
            else
            {
                Console.Write("Press any key to continue...");
                Console.ReadKey();
                Console.Clear();
            }
        }
    }

    private static double TimeToApm(TimeSpan timeUsed)
    {
        return 60.0 / timeUsed.TotalSeconds;
    }

    private static void AddToDict<T>(Dictionary<T, int> dict, T item,int cap) where T : notnull
    {
        var newTargetValue = Math.Min(dict.GetValueOrDefault(item, 0) + 1, cap);
        dict[item] = newTargetValue;
    }

    private static void RemoveFromDict<T>(Dictionary<T, int> dict, T item) where T : notnull
    {
        if (dict.TryGetValue(item, out var value))
        {
            if (value > 1)
            {
                dict[item] = value - 1;
            }
            else
            {
                dict.Remove(item);
            }
        }
    }

    private string PrintRemainingPastErrorAndRepeatInstanceNumbers()
    {
        var totalFixes = _pastErrors.Values.Sum();
        return $"{totalFixes} blocking question(s) remain for {_pastErrors.Count} incorrect sets.";
    }

    private string PrintRemainingPastSlowAnswers()
    {
        var totalSlowAnswers = _pastSlowAnswers.Values.Sum();
        return $"{totalSlowAnswers} slow answer(s) remain for {_pastSlowAnswers.Count} sets.";
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
        if (_pastSlowAnswers.Count > 0)
        {
            var pickSlow = rand.Next(0, 2);
            if (pickSlow == 1)
            {
                // Pick a random slow answer from past slow answers
                var slowAnswer = _pastSlowAnswers.ElementAt(rand.Next(_pastSlowAnswers.Count));
                var slowOperands = OrderOperands(slowAnswer.Key);
                return flip == 0 ? slowOperands : (slowOperands.Item2, slowOperands.Item1);
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