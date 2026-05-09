using Quartz;

namespace UniEmu.Runtime;

public static class RuntimeJobKeys
{
    public const string EmulatorId = "emulatorId";
    public const string TagId = "tagId";

    public static JobKey PublishJob(string emulatorId) => new($"publish-{emulatorId}", "uniemu-publish");

    public static TriggerKey PublishTrigger(string emulatorId) => new($"publish-{emulatorId}", "uniemu-publish");

    public static JobKey DispatcherBlockCheckJob(string emulatorId) => new($"dispatcher-block-{emulatorId}", "uniemu-dispatcher-block");

    public static TriggerKey DispatcherBlockCheckTrigger(string emulatorId) => new($"dispatcher-block-{emulatorId}", "uniemu-dispatcher-block");

    public static JobKey TagJob(string emulatorId, string tagId) => new($"tag-{tagId}", TagGroup(emulatorId));

    public static TriggerKey TagTrigger(string emulatorId, string tagId) => new($"tag-{tagId}", TagGroup(emulatorId));

    public static string TagGroup(string emulatorId) => $"uniemu-tags-{emulatorId}";
}
