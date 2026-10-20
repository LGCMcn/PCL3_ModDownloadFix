namespace MeloongCore.Extensions;
public static class TaskExtensions {

    public static void Run(this Task task) => task.GetAwaiter().GetResult();
    public static T Run<T>(this Task<T> task) => task.GetAwaiter().GetResult();

    public static ConfiguredTaskAwaitable NoCapture(this Task task) => task.ConfigureAwait(false);
    public static ConfiguredTaskAwaitable<T> NoCapture<T>(this Task<T> task) => task.ConfigureAwait(false);

}
