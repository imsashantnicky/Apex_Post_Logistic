using System.Collections.Frozen;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace ApexPost.UnitTests.Architecture;

/// <summary>
/// What compiled code really touches, found with plain reflection: signatures, attributes, locals and the metadata
/// tokens inside the IL. Assembly references alone are not enough, because System.IO lives in the core library that
/// every assembly references, and a static call such as File.ReadAllText leaves no trace in a signature.
/// </summary>
internal static class CodeUsage
{
    private const BindingFlags Declared =
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.DeclaredOnly;

    private const string AwaiterResult = "GetResult";

    private static readonly FrozenSet<string> BlockingCalls = new[]
    {
        "Task.Wait",
        "Task.WaitAll",
        "Task.WaitAny",
        "Task`1.get_Result",
        "ValueTask`1.get_Result",
        "Thread.Sleep",
    }.ToFrozenSet();

    private static readonly FrozenDictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToFrozenDictionary(code => code.Value);

    public static IReadOnlySet<Type> TypesUsedBy(Type type)
    {
        var used = new HashSet<Type>();
        foreach (var mentioned in Signatures(type).Concat(Methods(type).SelectMany(TypesInBody)))
        {
            AddWithTypeArguments(mentioned, used);
        }

        return used;
    }

    /// <summary>Blocking calls as "Owner -> Task.Wait". Only a state machine may read an awaiter's result.</summary>
    public static IEnumerable<string> BlockingCallsIn(IEnumerable<Type> types) =>
        from type in types
        from call in Methods(type).Where(IsWrittenCode).SelectMany(MembersInBody).OfType<MethodBase>()
        where Blocks(type, call)
        select $"{type.FullName} -> {call.DeclaringType?.Name}.{call.Name}";

    public static IEnumerable<string> AsyncVoidMethodsIn(IEnumerable<Type> types) =>
        from type in types
        from method in type.GetMethods(Declared)
        where method.ReturnType == typeof(void) && method.IsDefined(typeof(AsyncStateMachineAttribute), false)
        select $"{type.FullName}.{method.Name}";

    /// <summary>An async Program gets a compiler-made entry stub that must wait for it; nothing else is exempt.</summary>
    private static bool IsWrittenCode(MethodBase method) =>
        !(method == method.Module.Assembly.EntryPoint && method.Name.StartsWith('<'));

    private static bool Blocks(Type caller, MethodBase call)
    {
        var owner = call.DeclaringType?.Name ?? string.Empty;
        return BlockingCalls.Contains($"{owner}.{call.Name}")
            || (call.Name == AwaiterResult
                && owner.Contains("Awaiter", StringComparison.Ordinal)
                && !caller.IsAssignableTo(typeof(IAsyncStateMachine)));
    }

    private static IEnumerable<MethodBase> Methods(Type type) =>
        type.GetMethods(Declared).Concat<MethodBase>(type.GetConstructors(Declared));

    private static IEnumerable<Type?> Signatures(Type type)
    {
        MemberInfo[] members = type.GetMembers(Declared);
        return members
            .Append(type)
            .SelectMany(member => member.CustomAttributes)
            .Select(attribute => attribute.AttributeType)
            .Concat(type.GetInterfaces())
            .Append(type.BaseType)
            .Concat(type.GetFields(Declared).Select(field => field.FieldType))
            .Concat(type.GetProperties(Declared).Select(property => property.PropertyType))
            .Concat(type.GetEvents(Declared).Select(declared => declared.EventHandlerType))
            .Concat(Methods(type).SelectMany(TypesInSignature));
    }

    private static IEnumerable<Type?> TypesInSignature(MethodBase method) =>
        method.GetParameters()
            .Select(parameter => parameter.ParameterType)
            .Append((method as MethodInfo)?.ReturnType);

    private static IEnumerable<Type?> TypesInBody(MethodBase method)
    {
        var body = method.GetMethodBody();
        if (body is null)
        {
            return [];
        }

        return body.LocalVariables
            .Select(local => local.LocalType)
            .Concat(body.ExceptionHandlingClauses
                .Where(clause => clause.Flags == ExceptionHandlingClauseOptions.Clause)
                .Select(clause => clause.CatchType))
            .Concat(MembersInBody(method).SelectMany(TypesOfMember));
    }

    private static IEnumerable<Type?> TypesOfMember(MemberInfo member) => member switch
    {
        Type type => [type],
        FieldInfo field => [field.DeclaringType, field.FieldType],
        MethodBase method => TypesInSignature(method).Append(method.DeclaringType),
        _ => [member.DeclaringType],
    };

    private static IEnumerable<MemberInfo> MembersInBody(MethodBase method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray() ?? [];
        var typeArguments = method.DeclaringType is { IsGenericType: true } owner ? owner.GetGenericArguments() : null;
        var methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
        return MetadataTokens(il)
            .Select(token => method.Module.ResolveMember(token, typeArguments, methodArguments))
            .OfType<MemberInfo>();
    }

    private static List<int> MetadataTokens(byte[] il)
    {
        const byte TwoByteOpCodePrefix = 0xFE;
        var tokens = new List<int>();
        var offset = 0;
        while (offset < il.Length)
        {
            var value = il[offset] == TwoByteOpCodePrefix ? (short)((il[offset] << 8) | il[offset + 1]) : il[offset];
            var code = OpCodesByValue[value];
            offset += code.Size;
            if (code.OperandType is OperandType.InlineMethod or OperandType.InlineField
                or OperandType.InlineType or OperandType.InlineTok)
            {
                tokens.Add(BitConverter.ToInt32(il, offset));
            }

            offset += OperandSize(code.OperandType, il, offset);
        }

        return tokens;
    }

    private static int OperandSize(OperandType operand, byte[] il, int offset) => operand switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => sizeof(int) * (1 + BitConverter.ToInt32(il, offset)),
        _ => sizeof(int),
    };

    private static void AddWithTypeArguments(Type? type, HashSet<Type> used)
    {
        if (type is null || type.IsGenericParameter || !used.Add(type))
        {
            return;
        }

        AddWithTypeArguments(type.GetElementType(), used);
        foreach (var argument in type.GetGenericArguments())
        {
            AddWithTypeArguments(argument, used);
        }
    }
}
