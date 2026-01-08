namespace RSSTelegramBot;

public enum CreateFlowStep
{
    None,
    AwaitName,
    AwaitUrl
}

public record CreateFlowState(CreateFlowStep Step, string? Name);