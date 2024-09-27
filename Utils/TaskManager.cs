using HandyControl.Controls;
using HandyControl.Data;

namespace MFATools.Utils;

public static class TaskManager
{
    /// <summary>
    /// 执行任务, 并带有更好的日志显示
    /// </summary>
    /// <param name="action">要执行的动作</param>
    /// <param name="name">日志显示名称</param>
    /// <param name="prompt">日志提示</param>
    public static void RunTask(
        Action action, Action? handleError = null,
        string name = nameof(Action),
        bool catchException = true)
    {
        Console.WriteLine($"任务 {name} 开始.");
        LoggerService.LogInfo($"任务 {name} 开始.");

        try
        {
            // 执行任务
            action.Invoke();
            Console.WriteLine($"任务 {name} 完成.");
            LoggerService.LogInfo($"任务 {name} 完成.");
        }
        catch (Exception e)
        {
            Console.WriteLine($"任务 {name} 失败: {e.Message}");
            LoggerService.LogError($"任务 {name} 失败: {e.Message}\n{e.StackTrace}");
            if (catchException)
            {
                handleError?.Invoke();
                LoggerService.LogError(e.GetBaseException());
            }
            else throw;
        }
    }


    /// <summary>
    /// 异步执行任务, 并带有更好的日志显示
    /// </summary>
    /// <param name="action">要执行的动作</param>
    /// <param name="name">任务名称</param>
    /// <param name="prompt">日志提示</param>
    public static async Task RunTaskAsync(
        Action action, Action? handleError = null,
        string name = nameof(Action),
        bool catchException = true)
    {
        Console.WriteLine($"异步任务 {name} 开始.");
        LoggerService.LogInfo($"异步任务 {name} 开始.");
        try
        {
            var task = Task.Run(action);
            await task;
            Console.WriteLine($"异步任务 {name} 已完成.");
            LoggerService.LogInfo($"任务 {name} 已完成.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"异步任务 {name} 失败: {ex.GetBaseException().Message}");
            LoggerService.LogError($"任务 {name} 失败: {ex.Message}\n{ex.StackTrace}");
            if (catchException)
            {
                handleError?.Invoke();
                LoggerService.LogError(ex.GetBaseException());
            }
            else throw;
        }
    }
}