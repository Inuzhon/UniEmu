using Quartz;

namespace UniEmu.Runtime;

/// <summary>
/// Формирует стабильные ключи Quartz-задач и триггеров runtime-публикации UniEmu.
/// </summary>
public static class RuntimeJobKeys
{
    /// <summary>
    /// Имя параметра Quartz JobDataMap с идентификатором эмулятора.
    /// </summary>
    public const string EmulatorId = "emulatorId";

    /// <summary>
    /// Имя параметра Quartz JobDataMap с идентификатором тега.
    /// </summary>
    public const string TagId = "tagId";

    /// <summary>
    /// Создает ключ задачи публикации телеметрии эмулятора.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    /// <returns>Ключ Quartz-задачи публикации.</returns>
    public static JobKey PublishJob(string emulatorId) => new($"publish-{emulatorId}", "uniemu-publish");

    /// <summary>
    /// Создает ключ триггера публикации телеметрии эмулятора.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    /// <returns>Ключ Quartz-триггера публикации.</returns>
    public static TriggerKey PublishTrigger(string emulatorId) => new($"publish-{emulatorId}", "uniemu-publish");

    /// <summary>
    /// Создает ключ задачи проверки блокировки мониторинга Dispatcher.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    /// <returns>Ключ Quartz-задачи проверки блокировки.</returns>
    public static JobKey DispatcherBlockCheckJob(string emulatorId) => new($"dispatcher-block-{emulatorId}", "uniemu-dispatcher-block");

    /// <summary>
    /// Создает ключ триггера проверки блокировки мониторинга Dispatcher.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    /// <returns>Ключ Quartz-триггера проверки блокировки.</returns>
    public static TriggerKey DispatcherBlockCheckTrigger(string emulatorId) => new($"dispatcher-block-{emulatorId}", "uniemu-dispatcher-block");

    /// <summary>
    /// Создает ключ задачи вычисления отдельного тега.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    /// <param name="tagId">Идентификатор тега.</param>
    /// <returns>Ключ Quartz-задачи тега.</returns>
    public static JobKey TagJob(string emulatorId, string tagId) => new($"tag-{tagId}", TagGroup(emulatorId));

    /// <summary>
    /// Создает ключ триггера вычисления отдельного тега.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    /// <param name="tagId">Идентификатор тега.</param>
    /// <returns>Ключ Quartz-триггера тега.</returns>
    public static TriggerKey TagTrigger(string emulatorId, string tagId) => new($"tag-{tagId}", TagGroup(emulatorId));

    /// <summary>
    /// Возвращает имя группы Quartz-задач тегов одного эмулятора.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    /// <returns>Имя группы задач тегов.</returns>
    public static string TagGroup(string emulatorId) => $"uniemu-tags-{emulatorId}";
}
