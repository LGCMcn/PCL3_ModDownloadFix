using System.Security.Principal;

namespace MeloongCore;
public static class WindowsUtils {

    /// <summary>
    /// 当前程序是否拥有管理员权限。
    /// </summary>
    public static bool HasAdminRole() {
        using var id = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }

}
