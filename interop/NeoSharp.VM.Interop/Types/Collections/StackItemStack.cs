using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NeoSharp.VM.Interop.Extensions;
using NeoSharp.VM.Interop.Native;
using NeoSharp.VM.Interop.Types.StackItems;

namespace NeoSharp.VM.Interop.Types.Collections
{
    public class StackItemStack : Stack
    {
        /// <summary>
        /// Native handle
        /// </summary>
        private readonly IntPtr _handle;

        /// <summary>
        /// Engine
        /// </summary>
        private readonly ExecutionEngine _engine;

        /// <summary>
        /// Return the number of items in the stack
        /// </summary>
        public override int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_engine.IsDisposed) throw new ObjectDisposedException(nameof(ExecutionEngine));

                return NeoVM.StackItems_Count(_handle);
            }
        }

        /// <summary>
        /// Push object to the stack
        /// </summary>
        /// <param name="item">Object</param>
        public override void Push(StackItemBase item)
        {
            if (_engine.IsDisposed) throw new ObjectDisposedException(nameof(ExecutionEngine));

            NeoVM.StackItems_Push(_handle, ((INativeStackItem)item).Handle);
        }

        /// <summary>
        /// Try to obtain the element at `index` position, without consume them
        /// -1=Last , -2=Last-1 , -3=Last-2
        /// </summary>
        /// <param name="index">Index</param>
        /// <param name="obj">Object</param>
        /// <returns>Return tru eif object exists</returns>
        public override bool TryPeek(int index, out StackItemBase obj)
        {
            if (_engine.IsDisposed) throw new ObjectDisposedException(nameof(ExecutionEngine));

            var ptr = NeoVM.StackItems_Peek(_handle, index);

            if (ptr == IntPtr.Zero)
            {
                obj = null;
                return false;
            }

            obj = _engine.ConvertFromNative(ptr);
            return true;
        }

        /// <summary>
        /// Try Pop object casting to this type
        /// </summary>
        /// <typeparam name="TStackItem">Object type</typeparam>
        /// <param name="item">Item</param>
        /// <returns>Return false if it is something wrong</returns>
        public override bool TryPop(out StackItemBase item)
        {
            if (_engine.IsDisposed) throw new ObjectDisposedException(nameof(ExecutionEngine));

            var ptr = NeoVM.StackItems_Pop(_handle);

            if (ptr == IntPtr.Zero)
            {
                item = null;
                return false;
            }

            item = _engine.ConvertFromNative(ptr);
            return item != null;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="engine">Engine</param>
        /// <param name="handle">Handle</param>
        internal StackItemStack(ExecutionEngine engine, IntPtr handle)
        {
            _handle = handle;
            _engine = engine;

            if (handle == IntPtr.Zero) throw new ExternalException();
            if (_engine.IsDisposed) throw new ObjectDisposedException(nameof(ExecutionEngine));
        }

        #region Create items

        /// <summary>
        /// Create Map StackItem
        /// </summary>
        protected override MapStackItemBase CreateMap()
        {
            return new MapStackItem(_engine);
        }

        /// <summary>
        /// Create Array StackItem
        /// </summary>
        /// <param name="items">Items</param>
        protected override ArrayStackItemBase CreateArray(IEnumerable<StackItemBase> items = null)
        {
            return new ArrayStackItem(_engine, items, false);
        }

        /// <summary>
        /// Create Struct StackItem
        /// </summary>
        /// <param name="items">Items</param>
        protected override ArrayStackItemBase CreateStruct(IEnumerable<StackItemBase> items = null)
        {
            return new ArrayStackItem(_engine, items, true);
        }

        /// <summary>
        /// Create ByteArrayStackItem
        /// </summary>
        /// <param name="data">Buffer</param>
        protected override ByteArrayStackItemBase CreateByteArray(byte[] data)
        {
            return new ByteArrayStackItem(_engine, data);
        }

        /// <summary>
        /// Create InteropStackItem
        /// </summary>
        /// <param name="obj">Object</param>
        protected override InteropStackItemBase<T> CreateInterop<T>(T obj)
        {
            var objKey = _engine.PrepareInterop(obj);

            return new InteropStackItem<T>(_engine, obj, objKey);
        }

        /// <summary>
        /// Create BooleanStackItem
        /// </summary>
        /// <param name="value">Value</param>
        protected override BooleanStackItemBase CreateBool(bool value)
        {
            return new BooleanStackItem(_engine, value);
        }

        /// <summary>
        /// Create IntegerStackItem
        /// </summary>
        /// <param name="value">Value</param>
        protected override IntegerStackItemBase CreateInteger(int value)
        {
            return new IntegerStackItem(_engine, value);
        }

        /// <summary>
        /// Create IntegerStackItem
        /// </summary>
        /// <param name="value">Value</param>
        protected override IntegerStackItemBase CreateInteger(long value)
        {
            return new IntegerStackItem(_engine, value);
        }

        /// <summary>
        /// Create IntegerStackItem
        /// </summary>
        /// <param name="value">Value</param>
        protected override IntegerStackItemBase CreateInteger(BigInteger value)
        {
            return new IntegerStackItem(_engine, value);
        }

        #endregion

        /// <summary>
        /// String representation
        /// </summary>
        public override string ToString() => Count.ToString();
    }
}