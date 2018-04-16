﻿using NeoVM.Interop.Enums;
using NeoVM.Interop.Helpers;
using NeoVM.Interop.Interfaces;
using NeoVM.Interop.Types.Arguments;
using NeoVM.Interop.Types.Collections;
using NeoVM.Interop.Types.StackItems;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

namespace NeoVM.Interop.Types
{
    public unsafe class ExecutionEngine : IDisposable
    {
        #region Delegates

        NeoVM.OnStepIntoCallback _InternalOnStepInto;
        NeoVM.OnStackChangeCallback _InternalOnExecutionContextChange;
        NeoVM.OnStackChangeCallback _InternalOnAltStackChange;
        NeoVM.OnStackChangeCallback _InternalOnEvaluationStackChange;

        NeoVM.InvokeInteropCallback _InternalInvokeInterop;
        NeoVM.LoadScriptCallback _InternalLoadScript;
        NeoVM.GetMessageCallback _InternalGetMessage;

        #endregion

        /// <summary>
        /// Native handle
        /// </summary>
        IntPtr Handle;
        /// <summary>
        /// Last message
        /// </summary>
        byte[] LastMessage;
        /// <summary>
        /// Trigger
        /// </summary>
        public readonly ETriggerType Trigger;
        /// <summary>
        /// Interop service
        /// </summary>
        public readonly InteropService InteropService;
        /// <summary>
        /// Script table
        /// </summary>
        public readonly IScriptTable ScriptTable;
        /// <summary>
        /// Is Disposed
        /// </summary>
        public bool IsDisposed => Handle == IntPtr.Zero;

        /// <summary>
        /// Logger
        /// </summary>
        public readonly ExecutionEngineLogger Logger;
        /// <summary>
        /// Message Provider
        /// </summary>
        public readonly IMessageProvider MessageProvider;
        /// <summary>
        /// Invocation Stack
        /// </summary>
        public readonly ExecutionContextStack InvocationStack;
        /// <summary>
        /// Evaluation Stack
        /// </summary>
        public readonly StackItemStack EvaluationStack;
        /// <summary>
        /// Alt Stack
        /// </summary>
        public readonly StackItemStack AltStack;
        /// <summary>
        /// Virtual Machine State
        /// </summary>
        public EVMState State => (EVMState)NeoVM.ExecutionEngine_GetState(Handle);

        #region Shortcuts

        public ExecutionContext CurrentContext => InvocationStack.TryPeek(0, out ExecutionContext i) ? i : null;
        public ExecutionContext CallingContext => InvocationStack.TryPeek(1, out ExecutionContext i) ? i : null;
        public ExecutionContext EntryContext => InvocationStack.TryPeek(InvocationStack.Count - 1, out ExecutionContext i) ? i : null;

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="e">Arguments</param>
        public ExecutionEngine(ExecutionEngineArgs e)
        {
            _InternalInvokeInterop = new NeoVM.InvokeInteropCallback(InternalInvokeInterop);
            _InternalLoadScript = new NeoVM.LoadScriptCallback(InternalLoadScript);
            _InternalGetMessage = new NeoVM.GetMessageCallback(InternalGetMessage);

            Handle = NeoVM.ExecutionEngine_Create
                (
                _InternalInvokeInterop, _InternalLoadScript, _InternalGetMessage,
                out IntPtr invHandle, out IntPtr evHandle, out IntPtr altHandle
                );

            if (Handle == IntPtr.Zero)
                throw (new ExternalException());

            InvocationStack = new ExecutionContextStack(invHandle);
            EvaluationStack = new StackItemStack(this, evHandle);
            AltStack = new StackItemStack(this, altHandle);

            if (e != null)
            {
                InteropService = e.InteropService;
                ScriptTable = e.ScriptTable;
                MessageProvider = e.MessageProvider;
                Trigger = e.Trigger;

                // Register logs

                if (e.Logger != null)
                {
                    Logger = e.Logger;

                    if (Logger.Verbosity.HasFlag(ELogVerbosity.StepInto))
                    {
                        _InternalOnStepInto = new NeoVM.OnStepIntoCallback(InternalOnStepInto);
                        NeoVM.ExecutionEngine_AddLog(Handle, _InternalOnStepInto);
                    }

                    if (Logger.Verbosity.HasFlag(ELogVerbosity.ExecutionContextStackChanges))
                    {
                        _InternalOnExecutionContextChange = new NeoVM.OnStackChangeCallback(InternalOnExecutionContextChange);
                        NeoVM.ExecutionContextStack_AddLog(invHandle, _InternalOnExecutionContextChange);
                    }

                    if (Logger.Verbosity.HasFlag(ELogVerbosity.AltStackChanges))
                    {
                        _InternalOnAltStackChange = new NeoVM.OnStackChangeCallback(InternalOnAltStackChange);
                        NeoVM.StackItems_AddLog(altHandle, _InternalOnAltStackChange);
                    }

                    if (Logger.Verbosity.HasFlag(ELogVerbosity.EvaluationStackChanges))
                    {
                        _InternalOnEvaluationStackChange = new NeoVM.OnStackChangeCallback(InternalOnEvaluationStackChange);
                        NeoVM.StackItems_AddLog(evHandle, _InternalOnEvaluationStackChange);
                    }
                }
                else
                {
                    Logger = null;
                }
            }
        }

        /// <summary>
        /// Internal callback for OnStepInto
        /// </summary>
        /// <param name="it">Context</param>
        void InternalOnStepInto(IntPtr it)
        {
            using (ExecutionContext context = new ExecutionContext(it))
                Logger.RaiseOnStepInto(context);
        }
        /// <summary>
        /// Internal callback for OnExecutionContextChange
        /// </summary>
        /// <param name="it">Item</param>
        /// <param name="index">Index</param>
        /// <param name="operation">Operation</param>
        void InternalOnExecutionContextChange(IntPtr it, int index, byte operation)
        {
            using (ExecutionContext context = new ExecutionContext(it))
                Logger.RaiseOnExecutionContextChange(InvocationStack, context, index, (ELogStackOperation)operation);
        }
        /// <summary>
        /// Internal callback for OnAltStackChange
        /// </summary>
        /// <param name="item">Item</param>
        /// <param name="index">Index</param>
        /// <param name="operation">Operation</param>
        void InternalOnAltStackChange(IntPtr item, int index, byte operation)
        {
            using (IStackItem it = ConvertFromNative(item))
                Logger.RaiseOnAltStackChange(AltStack, it, index, (ELogStackOperation)operation);
        }
        /// <summary>
        /// Internal callback for OnEvaluationStackChange
        /// </summary>
        /// <param name="item">Item</param>
        /// <param name="index">Index</param>
        /// <param name="operation">Operation</param>
        void InternalOnEvaluationStackChange(IntPtr item, int index, byte operation)
        {
            using (IStackItem it = ConvertFromNative(item))
                Logger.RaiseOnEvaluationStackChange(EvaluationStack, it, index, (ELogStackOperation)operation);
        }
        /// <summary>
        /// Get message callback
        /// </summary>
        /// <param name="iteration">Iteration</param>
        /// <param name="output">Message</param>
        int InternalGetMessage(uint iteration, out IntPtr output)
        {
            if (MessageProvider != null)
            {
                // TODO: should change this, too dangerous

                byte[] script = MessageProvider.GetMessage(iteration);

                if (script != null && script.Length > 0)
                {
                    // Prevent dispose

                    LastMessage = script;

                    fixed (byte* p = LastMessage)
                    {
                        output = (IntPtr)p;
                    }

                    return LastMessage.Length;
                }
            }

            output = IntPtr.Zero;
            return 0;
        }
        /// <summary>
        /// Load script callback
        /// </summary>
        /// <param name="scriptHash">Hash</param>
        /// <param name="isDynamicInvoke">Is dynamic invoke</param>
        /// <returns>Return 0x01 if is corrected loaded</returns>
        byte InternalLoadScript(byte[] scriptHash, byte isDynamicInvoke)
        {
            if (ScriptTable == null)
            {
                return 0x00;
            }

            byte[] script = ScriptTable.GetScript(scriptHash, isDynamicInvoke == 0x01);

            if (script == null || script.Length <= 0)
            {
                return 0x00;
            }

            fixed (byte* p = script)
            {
                NeoVM.ExecutionEngine_LoadScript(Handle, (IntPtr)p, script.Length);
            }

            return 0x01;
        }
        /// <summary>
        /// Invoke Interop callback
        /// </summary>
        /// <param name="ptr">Method</param>
        /// <param name="size">Size</param>
        /// <returns>Return Interop result</returns>
        byte InternalInvokeInterop(IntPtr ptr, byte size)
        {
            if (InteropService == null)
                return 0x00;

            string method = Marshal.PtrToStringUTF8(ptr, size);
            if (InteropService.Invoke(method, this))
                return 0x01;

            return 0x00;
        }

        #region Load Script

        /// <summary>
        /// Load script
        /// </summary>
        /// <param name="script">Script</param>
        public void LoadScript(byte[] script)
        {
            fixed (byte* p = script)
            {
                NeoVM.ExecutionEngine_LoadScript(Handle, (IntPtr)p, script.Length);
            }
        }
        /// <summary>
        /// Load script
        /// </summary>
        /// <param name="script">Script</param>
        public void LoadPushOnlyScript(byte[] script)
        {
            fixed (byte* p = script)
            {
                NeoVM.ExecutionEngine_LoadPushOnlyScript(Handle, (IntPtr)p, script.Length);
            }
        }

        #endregion

        #region Execution

        /// <summary>
        /// Clean Execution engine state
        /// </summary>
        /// <param name="iteration">Iteration</param>
        public void Clean(uint iteration)
        {
            NeoVM.ExecutionEngine_Clean(Handle, iteration);
        }
        /// <summary>
        /// Execute
        /// </summary>
        public EVMState Execute()
        {
            return (EVMState)NeoVM.ExecutionEngine_Execute(Handle);
        }
        /// <summary>
        /// Step Into
        /// </summary>
        public void StepInto()
        {
            NeoVM.ExecutionEngine_StepInto(Handle);
        }
        /// <summary>
        /// Step Into
        /// </summary>
        /// <param name="steps">Steps</param>
        public void StepInto(int steps)
        {
            for (int x = 0; x < steps; x++)
                NeoVM.ExecutionEngine_StepInto(Handle);
        }
        /// <summary>
        /// Step Out
        /// </summary>
        public void StepOut()
        {
            NeoVM.ExecutionEngine_StepOut(Handle);
        }
        /// <summary>
        /// Step Over
        /// </summary>
        public void StepOver()
        {
            NeoVM.ExecutionEngine_StepOver(Handle);
        }

        #endregion

        #region Create items

        /// <summary>
        /// Convert native pointer to stack item
        /// </summary>
        /// <param name="item">Item</param>
        /// <returns>Return StackItem</returns>
        internal IStackItem ConvertFromNative(IntPtr item)
        {
            if (item == IntPtr.Zero) return null;

            EStackItemType state = (EStackItemType)NeoVM.StackItem_SerializeInfo(item, out int size);
            if (state == EStackItemType.None) return null;

            int readed;
            byte[] payload;

            if (size > 0)
            {
                payload = new byte[size];
                fixed (byte* p = payload)
                {
                    readed = NeoVM.StackItem_Serialize(item, (IntPtr)p, size);
                }
            }
            else
            {
                readed = 0;
                payload = null;
            }

            switch (state)
            {
                case EStackItemType.Array: return new ArrayStackItem(this, item, false);
                case EStackItemType.Struct: return new ArrayStackItem(this, item, true);
                case EStackItemType.Map: return new MapStackItem(this, item);
                case EStackItemType.Interop:
                    {
                        // Extract object

                        IntPtr ptr = new IntPtr(BitHelper.ToInt64(payload, 0));
                        return new InteropStackItem(this, item, Marshal.GetObjectForIUnknown(ptr), ptr);
                    }
                case EStackItemType.ByteArray: return new ByteArrayStackItem(this, item, payload ?? (new byte[] { }));
                case EStackItemType.Integer:
                    {
                        if (readed != size)
                        {
                            // TODO: Try to fix this issue with BigInteger
                            Array.Resize(ref payload, readed);
                        }

                        return new IntegerStackItem(this, item, payload ?? (new byte[] { }));
                    }
                case EStackItemType.Bool: return new BooleanStackItem(this, item, payload ?? (new byte[] { }));
                default: throw new ExternalException();
            }
        }

        /// <summary>
        /// Create Array StackItem
        /// </summary>
        public ArrayStackItem CreateArray()
        {
            return new ArrayStackItem(this, false);
        }
        /// <summary>
        /// Create Struct StackItem
        /// </summary>
        public ArrayStackItem CreateStruct()
        {
            return new ArrayStackItem(this, true);
        }
        /// <summary>
        /// Create Array StackItem
        /// </summary>
        /// <param name="items">Items</param>
        public ArrayStackItem CreateArray(IEnumerable<IStackItem> items)
        {
            return new ArrayStackItem(this, items, false);
        }
        /// <summary>
        /// Create Struct StackItem
        /// </summary>
        /// <param name="items">Items</param>
        public ArrayStackItem CreateStruct(IEnumerable<IStackItem> items)
        {
            return new ArrayStackItem(this, items, true);
        }
        /// <summary>
        /// Create ByteArrayStackItem
        /// </summary>
        /// <param name="data">Buffer</param>
        public ByteArrayStackItem CreateByteArray(byte[] data)
        {
            return new ByteArrayStackItem(this, data);
        }
        /// <summary>
        /// Create InteropStackItem
        /// </summary>
        /// <param name="obj">Object</param>
        public InteropStackItem CreateInterop(object obj)
        {
            return new InteropStackItem(this, obj);
        }
        /// <summary>
        /// Create BooleanStackItem
        /// </summary>
        /// <param name="value">Value</param>
        public BooleanStackItem CreateBool(bool value)
        {
            return new BooleanStackItem(this, value);
        }
        /// <summary>
        /// Create IntegerStackItem
        /// </summary>
        /// <param name="value">Value</param>
        public IntegerStackItem CreateInteger(int value)
        {
            return new IntegerStackItem(this, value);
        }
        /// <summary>
        /// Create IntegerStackItem
        /// </summary>
        /// <param name="value">Value</param>
        public IntegerStackItem CreateInteger(long value)
        {
            return new IntegerStackItem(this, value);
        }
        /// <summary>
        /// Create IntegerStackItem
        /// </summary>
        /// <param name="value">Value</param>
        public IntegerStackItem CreateInteger(BigInteger value)
        {
            return new IntegerStackItem(this, value);
        }
        /// <summary>
        /// Create IntegerStackItem
        /// </summary>
        /// <param name="value">Value</param>
        public IntegerStackItem CreateInteger(byte[] value)
        {
            return new IntegerStackItem(this, value);
        }

        #endregion

        #region IDisposable Support

        protected virtual void Dispose(bool disposing)
        {
            if (Handle == IntPtr.Zero) return;

            // free unmanaged resources (unmanaged objects) and override a finalizer below. set large fields to null.
            NeoVM.ExecutionEngine_Free(ref Handle);
        }

        ~ExecutionEngine()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // uncomment the following line if the finalizer is overridden above.
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}