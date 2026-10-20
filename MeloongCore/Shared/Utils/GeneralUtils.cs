namespace MeloongCore;
public static class GeneralUtils {

    /// <summary>
    /// 判断对象是否为指定泛型类型的实例。
    /// </summary>
    public static bool IsGenericInstanceOf(object? instance, Type genericTypeDefinition) {
        if (instance is null) return false;
        for (var type = instance.GetType(); type is not null; type = type.BaseType) {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == genericTypeDefinition) return true;
        }
        return false;
    }

}
