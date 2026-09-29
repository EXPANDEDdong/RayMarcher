using System.Reflection;

namespace RayMarcher.Framework;

internal static class FrameSystemDiscovery
{
    private const BindingFlags MethodFlags =
        BindingFlags.Instance | BindingFlags.Static |
        BindingFlags.Public   | BindingFlags.NonPublic;

    public static IReadOnlyList<Type> DiscoverTypes(Assembly assembly) =>
        assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
            .Where(t => t.GetCustomAttribute<FrameSystemAttribute>() is not null)
            .ToArray();

    public static FrameSystemBindings Collect(object instance)
    {
        var type = instance.GetType();
        var cls  = type.GetCustomAttribute<FrameSystemAttribute>()
                   ?? throw new InvalidOperationException(
                       $"{type.Name} has no [FrameSystem] attribute.");

        var ticks  = new List<TickEntry>();
        var setups = new List<SetupEntry>();

        foreach (var method in type.GetMethods(MethodFlags))
        {
            var tm = method.GetCustomAttribute<TickMethodAttribute>();
            if (tm is not null)
            {
                ticks.Add(new TickEntry(
                    Tick:  BindTick(instance, method),
                    Phase: tm.Phase == Phase.Undefined ? cls.Phase : tm.Phase,
                    Order: tm.Order == -999 ? cls.Order : tm.Order,
                    Name:  $"{type.Name}.{method.Name}"));
            }

            var sm = method.GetCustomAttribute<SetupMethodAttribute>();
            if (sm is not null)
            {
                setups.Add(new SetupEntry(
                    Setup: BindSetup(instance, method),
                    Phase: sm.Phase,
                    Order: sm.Order,
                    Name:  $"{type.Name}.{method.Name}"));
            }
        }

        if (ticks.Count == 0 && setups.Count == 0)
            throw new InvalidOperationException(
                $"{type.Name} is marked [FrameSystem] but has no [TickMethod] or [SetupMethod]. " +
                "It will never run — did you forget the method attribute?");

        return new FrameSystemBindings(ticks, setups);
    }

    private static Action<float> BindTick(object instance, MethodInfo m)
    {
        string Where() => $"{m.DeclaringType!.Name}.{m.Name}";

        if (m.ReturnType != typeof(void))
            throw new InvalidOperationException($"{Where()} must return void.");
        if (m.IsGenericMethodDefinition)
            throw new InvalidOperationException($"{Where()} cannot be generic.");

        var target = m.IsStatic ? null : instance;
        var ps = m.GetParameters();

        if (ps.Length == 1 && ps[0].ParameterType == typeof(float))
            return (Action<float>)Delegate.CreateDelegate(typeof(Action<float>), target, m);

        if (ps.Length == 0)
        {
            var bound = (Action)Delegate.CreateDelegate(typeof(Action), target, m);
            return _ => bound();
        }

        throw new InvalidOperationException(
            $"{Where()} must be 'void M()' or 'void M(float dt)', but is '{m}'.");
    }
    
    private static Action BindSetup(object instance, MethodInfo m)
    {
        string Where() => $"{m.DeclaringType!.Name}.{m.Name}";

        if (m.ReturnType != typeof(void))
            throw new InvalidOperationException($"{Where()} must return void.");
        if (m.IsGenericMethodDefinition)
            throw new InvalidOperationException($"{Where()} cannot be generic.");
        if (m.GetParameters().Length != 0)
            throw new InvalidOperationException($"{Where()} must be 'void M()', but is '{m}'.");

        var target = m.IsStatic ? null : instance;
        return (Action)Delegate.CreateDelegate(typeof(Action), target, m);
    }
}