namespace Tests;

[UnitTest]
public class MainTests
{

    public MainTests(ITestOutputHelper outputHelper)
    {
        OutputHelper = outputHelper;
    }
  
    [Fact]
    public async Task ExecuteAsync_ResultsInSomething()
    {
        // Arrange
        var services = new ServiceCollection()
                     .AddLogging((builder) => builder.AddXUnit(OutputHelper))
                     .AddSingleton<TestSubject>();

        var sut = services
            .BuildServiceProvider()
            .GetRequiredService<TestSubject>();

        // Act
        int actual = sut.GetResult();

        // Assert
        actual.ShouldBe(1);

    }

    [Fact]
    public void DoSomething_LogsMessage()
    {
        // Arrange
        var loggerFactory = TestLoggerFactory.Create();

        var logger = loggerFactory.CreateLogger<LoggingSample>();
        var sample = new LoggingSample(logger);

        // Act
        sample.DoSomething();

        // Assert
        var log = Assert.Single(loggerFactory.Sink.LogEntries);
        // Assert the message rendered by a default formatter
        Assert.Equal("The answer is 42", log.Message);
    }

    public ITestOutputHelper OutputHelper { get; }

}

#region Subjects
public class TestSubject
{
    public int GetResult()
    {
        return 1;
    }
}

public class LoggingSample
{
    private readonly ILogger<LoggingSample> _logger;

    public LoggingSample(ILogger<LoggingSample> logger)
    {
        _logger = logger;
    }

    public void DoSomething()
    {
        _logger.LogInformation("The answer is {number}", 42);
    }

    public void DoExceptional()
    {
        var exception = new ArgumentNullException("foo");
        _logger.LogError(exception, "There was a {error}", "problem");
    }
}
#endregion
