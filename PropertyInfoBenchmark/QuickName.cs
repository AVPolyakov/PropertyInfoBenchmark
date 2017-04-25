using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace PropertyInfoBenchmark
{
	public static class QuickName
	{
		public static PropertyInfo GetProperyInfo<T, TPropery>(Func<T, TPropery> func)
		{
			var methodInfo = func.Method;
			PropertyInfo value;
			if (dictionary.TryGetValue(methodInfo, out value)) return value;
			var tuples = IlReader.Read(methodInfo).ToList();
			if (!tuples.Select(_ => _.Item1).SequenceEqual(new[] {OpCodes.Ldarg_1, OpCodes.Callvirt, OpCodes.Ret}))
				throw new ArgumentException($"The {nameof(func)} should encapsulate a method with a body that " +
					"consists of a sequence of intermediate language instructions " +
					$"{nameof(OpCodes.Ldarg_1)}, {nameof(OpCodes.Callvirt)}, {nameof(OpCodes.Ret)}.", nameof(func));
			return ResolveAndCachePropertyInfo(methodInfo, tuples[1].Item2.Value);
		}

		public static MemberInfo GetMemberInfo<TPropery>(Func<TPropery> func)
		{
			var methodInfo = func.Method;
			PropertyInfo value;
			if (dictionary.TryGetValue(methodInfo, out value)) return value;
			var tuples = IlReader.Read(methodInfo).ToList();
			var codes = tuples.Select(_ => _.Item1).ToList();
			if (codes.SequenceEqual(new[] {OpCodes.Call, OpCodes.Ret}))
				return ResolveAndCachePropertyInfo(methodInfo, tuples[0].Item2.Value);
			else if (codes.SequenceEqual(new[] {OpCodes.Ldarg_0, OpCodes.Ldfld, OpCodes.Ret}))
				return ResolveAndCacheFieldInfo(methodInfo, tuples[1].Item2.Value);
			else
				throw new ArgumentException($"The {nameof(func)} should encapsulate a method with a body that " +
					"consists of a sequence of intermediate language instructions " +
					$"{nameof(OpCodes.Call)}, {nameof(OpCodes.Ret)} or " +
					$"{nameof(OpCodes.Ldarg_0)}, {nameof(OpCodes.Ldfld)}, {nameof(OpCodes.Ret)}.", nameof(func));
		}

		private static MemberInfo ResolveAndCacheFieldInfo(MethodInfo methodInfo, int metadataToken)
		{
			var fieldInfo = methodInfo.Module.ResolveField(metadataToken,
				methodInfo.DeclaringType.GetGenericArguments(), null);
			fieldDictionary.TryAdd(methodInfo, fieldInfo);
			return fieldInfo;
		}

		private static readonly ConcurrentDictionary<MethodInfo, FieldInfo> fieldDictionary =
			new ConcurrentDictionary<MethodInfo, FieldInfo>();

		private static PropertyInfo ResolveAndCachePropertyInfo(MethodInfo methodInfo, int metadataToken)
		{
			var methodBase = methodInfo.Module.ResolveMethod(metadataToken,
				methodInfo.DeclaringType.GetGenericArguments(), null);
			Dictionary<MethodBase, PropertyInfo> infos;
			if (!propertyDictionary.TryGetValue(methodBase.DeclaringType, out infos))
			{
				infos = methodBase.DeclaringType.GetProperties().ToDictionary(_ => {
					MethodBase method = _.GetGetMethod();
					return method;
				});
				propertyDictionary.TryAdd(methodBase.DeclaringType, infos);
			}
			var propertyInfo = infos[methodBase];
			dictionary.TryAdd(methodInfo, propertyInfo);
			return propertyInfo;
		}

		private static readonly ConcurrentDictionary<MethodInfo, PropertyInfo> dictionary =
			new ConcurrentDictionary<MethodInfo, PropertyInfo>();

		private static readonly ConcurrentDictionary<Type, Dictionary<MethodBase, PropertyInfo>> propertyDictionary =
			new ConcurrentDictionary<Type, Dictionary<MethodBase, PropertyInfo>>();
	}

	/// <summary>
    /// http://www.codeproject.com/KB/cs/sdilreader.aspx
    /// </summary>
    internal static class IlReader
    {
        public static IEnumerable<Tuple<OpCode, int?>> Read(MethodBase methodInfo)
        {
            var methodBody = methodInfo.GetMethodBody();
            if (methodBody == null) yield break;
            var ilAsByteArray = methodBody.GetILAsByteArray();
            var position = 0;
            while (position < ilAsByteArray.Length)
            {
                OpCode opCode;
                ushort value = ilAsByteArray[position++];
                if (value == 0xfe)
                {
                    value = ilAsByteArray[position++];
                    opCode = multiByteOpCodes[value];
                }
                else
                    opCode = singleByteOpCodes[value];
                var metadataToken = Read(opCode, ilAsByteArray, ref position);
                yield return Tuple.Create(opCode, metadataToken);
            }
        }

        private static int? Read(OpCode opCode, byte[] ilAsByteArray, ref int position)
        {
            switch (opCode.OperandType)
            {
                case OperandType.InlineBrTarget:
                    ReadInt32(ilAsByteArray, ref position);
                    return new int?();
                case OperandType.InlineField:
                    return ReadInt32(ilAsByteArray, ref position);
                case OperandType.InlineMethod:
                    return ReadInt32(ilAsByteArray, ref position);
                case OperandType.InlineSig:
                    ReadInt32(ilAsByteArray, ref position);
                    return new int?();
                case OperandType.InlineTok:
                    ReadInt32(ilAsByteArray, ref position);
                    return new int?();
                case OperandType.InlineType:
                    ReadInt32(ilAsByteArray, ref position);
                    return new int?();
                case OperandType.InlineI:
                    ReadInt32(ilAsByteArray, ref position);
                    return new int?();
                case OperandType.InlineI8:
                    ReadInt64(ref position);
                    return new int?();
                case OperandType.InlineNone:
                    return new int?();
                case OperandType.InlineR:
                    ReadDouble(ref position);
                    return new int?();
                case OperandType.InlineString:
                    ReadInt32(ilAsByteArray, ref position);
                    return new int?();
                case OperandType.InlineSwitch:
                    var count = ReadInt32(ilAsByteArray, ref position);
                    for (var i = 0; i < count; i++) ReadInt32(ilAsByteArray, ref position);
                    return new int?();
                case OperandType.InlineVar:
                    ReadUInt16(ref position);
                    return new int?();
                case OperandType.ShortInlineBrTarget:
                    ReadSByte(ref position);
                    return new int?();
                case OperandType.ShortInlineI:
                    ReadSByte(ref position);
                    return new int?();
                case OperandType.ShortInlineR:
                    ReadSingle(ref position);
                    return new int?();
                case OperandType.ShortInlineVar:
                    ReadByte(ref position);
                    return new int?();
                default:
                    throw new InvalidOperationException();
            }
        }

        private static void ReadUInt16(ref int position) => position += 2;
        private static int ReadInt32(byte[] bytes, ref int position) 
            => bytes[position++] | bytes[position++] << 8 | bytes[position++] << 0x10 | bytes[position++] << 0x18;
        private static void ReadInt64(ref int position) => position += 8;
        private static void ReadDouble(ref int position) => position += 8;
        private static void ReadSByte(ref int position) => position++;
        private static void ReadByte(ref int position) => position++;
        private static void ReadSingle(ref int position) => position += 4;

        static IlReader()
        {
            singleByteOpCodes = new OpCode[0x100];
            multiByteOpCodes = new OpCode[0x100];
            foreach (var fieldInfo in typeof (OpCodes).GetFields())
                if (fieldInfo.FieldType == typeof (OpCode))
                {
                    var opCode = (OpCode) fieldInfo.GetValue(null);
                    var value = unchecked((ushort) opCode.Value);
                    if (value < 0x100)
                        singleByteOpCodes[value] = opCode;
                    else
                    {
                        if ((value & 0xff00) != 0xfe00)
                            throw new ApplicationException("Invalid OpCode.");
                        multiByteOpCodes[value & 0xff] = opCode;
                    }
                }
        }

        private static readonly OpCode[] multiByteOpCodes;
        private static readonly OpCode[] singleByteOpCodes;
    }
}