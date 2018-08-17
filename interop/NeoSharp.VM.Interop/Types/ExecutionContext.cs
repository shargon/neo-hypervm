﻿using System;
using NeoSharp.VM.Interop.Types.Collections;
using Newtonsoft.Json;

namespace NeoSharp.VM.Interop.Types
{
    unsafe public class ExecutionContext : IExecutionContext
    {
        #region Private fields

        // This delegates are required for native calls, 
        // otherwise is disposed and produce a memory error

        private byte[] _ScriptHash;

        private readonly IStackItemsStack _AltStack;
        private readonly IStackItemsStack _EvaluationStack;

        /// <summary>
        /// Native handle
        /// </summary>
        private IntPtr Handle;

        #endregion

        #region Public fields

        /// <summary>
        /// Engine
        /// </summary>
        [JsonIgnore]
        public new readonly ExecutionEngine Engine;

        /// <summary>
        /// Is Disposed
        /// </summary>
        public override bool IsDisposed => Handle == IntPtr.Zero;

        /// <summary>
        /// Next instruction
        /// </summary>
        public override EVMOpCode NextInstruction => (EVMOpCode)NeoVM.ExecutionContext_GetNextInstruction(Handle);

        /// <summary>
        /// Get Instruction pointer
        /// </summary>
        public override int InstructionPointer => NeoVM.ExecutionContext_GetInstructionPointer(Handle);

        /// <summary>
        /// Script Hash
        /// </summary>
        public override byte[] ScriptHash
        {
            get
            {
                if (_ScriptHash != null)
                {
                    return _ScriptHash;
                }

                _ScriptHash = new byte[ScriptHashLength];

                fixed (byte* p = _ScriptHash)
                {
                    if (NeoVM.ExecutionContext_GetScriptHash(Handle, (IntPtr)p, 0) != ScriptHashLength)
                    {
                        throw (new AccessViolationException());
                    }
                }

                return _ScriptHash;
            }
        }

        /// <summary>
        /// AltStack
        /// </summary>
        public override IStackItemsStack AltStack => _AltStack;

        /// <summary>
        /// EvaluationStack
        /// </summary>
        public override IStackItemsStack EvaluationStack => _EvaluationStack;

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="engine">Engine</param>
        /// <param name="handle">Handle</param>
        internal ExecutionContext(ExecutionEngine engine, IntPtr handle) : base(engine)
        {
            Handle = handle;
            Engine = engine;
            NeoVM.ExecutionContext_Claim(Handle, out IntPtr evHandle, out IntPtr altHandle);

            _AltStack = new StackItemStack(Engine, altHandle);
            _EvaluationStack = new StackItemStack(Engine, evHandle);
        }

        #region IDisposable Support

        protected override void Dispose(bool disposing)
        {
            if (Handle == IntPtr.Zero) return;

            // free unmanaged resources (unmanaged objects) and override a finalizer below. set large fields to null.

            _AltStack.Dispose();
            _EvaluationStack.Dispose();

            NeoVM.ExecutionContext_Free(ref Handle);
        }

        #endregion
    }
}