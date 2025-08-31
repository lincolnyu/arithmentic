using System.Reflection.Metadata.Ecma335;

{
    const double DefaultNonCarryAdditionCoeff = 0.8;
    const double DefaultCarryAdditionCoeff = 1.5;

    string configFilePath = "multiplier.cfg";
    if (args.Length == 1)
    {
        configFilePath = args[0];
    }

    string? strDigitsOperand1;
    string? strDigitsOperand2;
    string? strAnswersPerMin;
    string? strConsecutiveCorrect;
    string? strNonCarryAdditionCoeff;
    string? strCarryAdditionCoeff;
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

        strNonCarryAdditionCoeff = sr.ReadLine();
        strCarryAdditionCoeff = sr.ReadLine();

        strNonRepeatQueueLength = sr.ReadLine();
        strReinforceRepeatCap = sr.ReadLine();
    }

    var digitsOperand1 = int.Parse(strDigitsOperand1!);
    var digitsOperand2 = int.Parse(strDigitsOperand2!);
    var answersPerMin = double.Parse(strAnswersPerMin!);
    var consecutiveCorrect = int.Parse(strConsecutiveCorrect!);

    double nonCarryAdditionCoeff = DefaultNonCarryAdditionCoeff;
    if (strNonCarryAdditionCoeff is not null)
    {
        nonCarryAdditionCoeff = double.Parse(strNonCarryAdditionCoeff!);
    }

    double carryAdditionCoeff = DefaultCarryAdditionCoeff;
    if (strCarryAdditionCoeff is not null)
    {
        carryAdditionCoeff = double.Parse(strCarryAdditionCoeff!);
    }

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

    var game = new Game(digitsOperand1, digitsOperand2, answersPerMin, consecutiveCorrect, nonCarryAdditionCoeff, carryAdditionCoeff, nonRepeatQueueLength, reinforceRepeatCap);
    game.Start(logging);
    if (logging)
    {
        using var sw = new StreamWriter(loggerFilePath!);
        foreach (var answer in game.LoggedAnswers)
        {
            sw.WriteLine(answer);
        }
        Console.WriteLine($"Answers logged in file '{loggerFilePath}'.");
        System.Diagnostics.Process.Start("notepad.exe", loggerFilePath!);
    }
    Console.Write("Press any key to exit...");
    Console.ReadKey();
}

internal class Answer((int, int) operands, int answer, TimeSpan timeSpent, double performanceRatio)
{
    public (int, int) Operands { get; } = operands;
    public int AnswerValue { get; } = answer;
    public bool IsCorrect => Operands.Item1 * Operands.Item2 == AnswerValue;
    public TimeSpan TimeSpent { get; } = timeSpent;

    public double PerformanceRatio { get; } = performanceRatio;

    public static double PerformanceRatioToReportRatio(double performanceRatio)
    {
        return (performanceRatio - 1) * 100.0;
    }

    public override string ToString()
    {
        var question = $"{Operands.Item1}×{Operands.Item2}";
        var sign = IsCorrect ? "=" : "≠";
        var reportRatio = PerformanceRatioToReportRatio(PerformanceRatio);
        return $"{Operands.Item1}×{Operands.Item2}{sign}{AnswerValue},{TimeSpent.TotalSeconds:F2},{reportRatio:F2}%";
    }
}

internal class Game(int digitsOperand1, int digitsOperand2, double answersPerMin, int consecutiveCorrect, double nonCarryAdditionCoeff, double carryAdditionCoeff, int nonRepeatQueueLength, int reinforceRepeatCap)
{
    private readonly Dictionary<(int, int), int> _pastErrors = [];
    private readonly Dictionary<(int, int), int> _pastSlowAnswers = [];
    private readonly Dictionary<(int, int), int> _pastMaxWeakness = [];

    public int DigitsOperand1 { get; } = digitsOperand1;

    public int DigitsOperand2 { get; } = digitsOperand2;

    public double MinAnswersPerMinRequired { get; } = answersPerMin;

    public int ConsecutiveSuccessesRequired { get; } = consecutiveCorrect;

    public double NonCarryAdditionCoeff { get; } = nonCarryAdditionCoeff;

    public double CarryAdditionCoeff { get; } = carryAdditionCoeff;

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

        var referenceSingleDigitTime = TimeSpan.FromSeconds(60.0 / MinAnswersPerMinRequired);

        int consecutiveSuccess = 0;

        (double, int, int)? minPerf = null;
        (double, int, int)? maxPerf = null;
        
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
            var allowedTime = referenceSingleDigitTime * AssessComplexity(operand1, operand2, NonCarryAdditionCoeff, CarryAdditionCoeff);
            var perfRatio = allowedTime.TotalSeconds / timeUsed.TotalSeconds;

            if (int.TryParse(userAnswer, out var parsedAnswer) && parsedAnswer == answer)
            {
                var withinTimeLimit = timeUsed <= allowedTime;
                if (withinTimeLimit)
                {
                    consecutiveSuccess++;
                    minPerf = minPerf == null || perfRatio < minPerf.Value.Item1 ? (perfRatio, operand1, operand2) : minPerf;
                    maxPerf = maxPerf == null || perfRatio > maxPerf.Value.Item1 ? (perfRatio, operand1, operand2) : maxPerf;
                    RemoveFromDict(_pastSlowAnswers, distintOperandTuple);
                }
                else
                {                     
                    consecutiveSuccess = 0;
                    minPerf = null;
                    maxPerf = null;
                }
                
                RemoveFromDict(_pastErrors, distintOperandTuple);
                if (!withinTimeLimit)
                {
                    AddToDict(_pastSlowAnswers, distintOperandTuple, ReinforceRepeatCap);
                }

                string messageForCorrectAnswer = $"Correct taking {timeUsed.TotalSeconds:F2}s, perf ratio {Answer.PerformanceRatioToReportRatio(perfRatio):F2}%";
                if (withinTimeLimit)
                {
                    messageForCorrectAnswer += $" (Within time limit {allowedTime.TotalSeconds:F2}s).";
                }
                else
                {
                    messageForCorrectAnswer += $" (took too long for {allowedTime.TotalSeconds:F2}s).";
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

                done = (_pastErrors.Count == 0 && _pastSlowAnswers.Count == 0 && consecutiveSuccess >= ConsecutiveSuccessesRequired);

                Console.WriteLine($"{messageForCorrectAnswer}");
            }
            else
            {
                consecutiveSuccess = 0;
                minPerf = null;
                maxPerf = null;
                AddToDict(_pastErrors, distintOperandTuple, ReinforceRepeatCap);
                Console.WriteLine(
                    $"Incorrect! ({PrintRemainingPastErrorAndRepeatInstanceNumbers()})");
            }

            if (logging)
            {
                var answerEntry = new Answer((operand1, operand2), parsedAnswer, timeUsed, perfRatio);
                LoggedAnswers.Add(answerEntry);
            }

            Console.Write("Press any key to continue...");
            Console.ReadKey();
            Console.Clear();
        }

        if (done)
        {
            Console.Clear();
            Console.WriteLine($"Congratulations! You succeeded {consecutiveSuccess} times in a row above required {MinAnswersPerMinRequired:F2} A/min.");
            if (maxPerf.HasValue && minPerf.HasValue)
            {
                Console.WriteLine($"Max performance ratio: {Answer.PerformanceRatioToReportRatio(maxPerf.Value.Item1):F2}% ({maxPerf.Value.Item2}×{maxPerf.Value.Item3}).");
                Console.WriteLine($"Min performance ratio: {Answer.PerformanceRatioToReportRatio(minPerf.Value.Item1):F2}% ({minPerf.Value.Item2}×{minPerf.Value.Item3}).");
            }
        }
    }

    private static double TimeToApm(TimeSpan timeUsed)
    {
        return 60.0 / timeUsed.TotalSeconds;
    }

    private void AddToDict(Dictionary<(int,int), int> dict, (int,int) item,int cap)
    {
        var pastWeakness = _pastMaxWeakness.GetValueOrDefault(item, 0);
        var newTargetValue = dict.GetValueOrDefault(item, 0) + 1;
        if (pastWeakness > newTargetValue)
        {
            newTargetValue = pastWeakness;
        }
        newTargetValue = Math.Min(newTargetValue, cap);
        dict[item] = newTargetValue;
        _pastMaxWeakness[item] = Math.Max(newTargetValue, pastWeakness);
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

    // a<=b
    static double AssessComplexitySingleDigit(int a, int b)
    {
        if (a > b) (a, b) = (b, a);
        if (a == 6 && b > 6) return 1.1;
        if (a == 7 && b > 7) return 1.1;
        if (a == 5 && b == 9) return 1.0;
        if (a == 0) return 0.1;
        return 0.9;
    }

    static double AssessComplexity(int a, int b, double nonCarryAdditionCoeff, double carryAdditionCoeff)
    {
        if (a < b) (a, b) = (b, a);
        var digitsA = ConvertNumberToDigits(a);
        var digitsB = ConvertNumberToDigits(b);
        return AssessComplexity(digitsA, digitsB, nonCarryAdditionCoeff, carryAdditionCoeff);
    }

    static int[] ConvertNumberToDigits(int number)
    {
        Queue<int> q = new Queue<int>();
        for (; number > 0; number /= 10)
        {
            var d = number % 10;
            q.Enqueue(d);
        }
        return q.ToArray();
    }

    static double AssessComplexity(int[] digitsOperand1, int[] digitsOperand2, double nonCarryAdditionCoeff, double carryAdditionCoeff)
    {
        double totalComplexity = 0;
        foreach (var b in digitsOperand2)
        {
            for (var j = 0; j < digitsOperand1.Length; j++)
            {
                var a = digitsOperand1[j];

                var compSingleDigit = AssessComplexitySingleDigit(a, b);
                if (j < digitsOperand1.Length - 1)
                {
                    var m = a * b;
                    var carry = m / 10;

                    var lsd = digitsOperand1[j + 1] % 10;

                    if (carry >= 0)
                    {
                        // TODO Can make this configurable
                        if (carry + lsd < 10)
                        {
                            compSingleDigit += nonCarryAdditionCoeff;
                        }
                        else
                        {
                            compSingleDigit += carryAdditionCoeff;
                        }
                    }
                }

                totalComplexity += compSingleDigit;
            }
        }
        return totalComplexity;
    }
}