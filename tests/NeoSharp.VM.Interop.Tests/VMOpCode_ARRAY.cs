using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoSharp.VM.Interop.Types.StackItems;

namespace NeoSharp.VM.Interop.Tests
{
    [TestClass]
    public class VMOpCode_ARRAY : VMOpCodeTest
    {
        [TestMethod]
        public void HASKEY()
        {
            // Without push

            using (var script = new ScriptBuilder
            (
                EVMOpCode.HASKEY
            ))
            using (var engine = CreateEngine(Args))
            {
                // Load script

                engine.LoadScript(script);

                // Execute

                Assert.IsFalse(engine.Execute());

                // Check

                CheckClean(engine, false);
            }

            // Wrong type (1)

            using (var script = new ScriptBuilder
            (
                EVMOpCode.PUSH1,
                EVMOpCode.NEWMAP,
                EVMOpCode.HASKEY
            ))
            using (var engine = CreateEngine(Args))
            {
                // Load script

                engine.LoadScript(script);

                // Execute

                Assert.IsFalse(engine.Execute());

                // Check

                Assert.AreEqual(engine.CurrentContext.EvaluationStack.Count, 1);

                using (var i = engine.CurrentContext.EvaluationStack.PopObject<IntegerStackItem>())
                {
                    Assert.AreEqual(i.Value, 0x01);
                }

                CheckClean(engine, false);
            }

            // Wrong type (2)

            using (var script = new ScriptBuilder
            (
                EVMOpCode.PUSH1,
                EVMOpCode.PUSH1,
                EVMOpCode.HASKEY
            ))
            using (var engine = CreateEngine(Args))
            {
                // Load script

                engine.LoadScript(script);

                // Execute

                Assert.IsFalse(engine.Execute());

                // Check

                CheckClean(engine, false);
            }

            // Wrong index (-1)

            foreach (var isStruct in new bool[] { true, false })
                using (var script = new ScriptBuilder
                (
                    EVMOpCode.PUSH0,
                    isStruct ? EVMOpCode.NEWSTRUCT : EVMOpCode.NEWARRAY,
                    EVMOpCode.PUSHM1,
                    EVMOpCode.HASKEY
                ))
                using (var engine = CreateEngine(Args))
                {
                    // Load script

                    engine.LoadScript(script);

                    // Execute

                    Assert.IsFalse(engine.Execute());

                    // Check

                    CheckClean(engine, false);
                }

            // Real test 

            foreach (var isStruct in new bool[] { true, false })
                using (var script = new ScriptBuilder
                (
                    EVMOpCode.PUSH1,
                    isStruct ? EVMOpCode.NEWSTRUCT : EVMOpCode.NEWARRAY,
                    EVMOpCode.DUP,

                    EVMOpCode.PUSH0,
                    EVMOpCode.HASKEY,
                    EVMOpCode.TOALTSTACK,

                    EVMOpCode.PUSH1,
                    EVMOpCode.HASKEY,
                    EVMOpCode.FROMALTSTACK,
                    EVMOpCode.RET
                ))
                using (var engine = CreateEngine(Args))
                {
                    // Load script

                    engine.LoadScript(script);

                    // Execute

                    Assert.IsTrue(engine.Execute());

                    // Check

                    using (var i = engine.ResultStack.PopObject<BooleanStackItem>())
                    {
                        Assert.IsTrue(i.Value);
                    }

                    using (var i = engine.ResultStack.PopObject<BooleanStackItem>())
                    {
                        Assert.IsFalse(i.Value);
                    }

                    CheckClean(engine);
                }
        }

        [TestMethod]
        public void KEYS()
        {
            // Without push

            using (var script = new ScriptBuilder
            (
                EVMOpCode.KEYS
            ))
            using (var engine = CreateEngine(Args))
            {
                // Load script

                engine.LoadScript(script);

                // Execute

                Assert.IsFalse(engine.Execute());

                // Check

                CheckClean(engine, false);
            }

            // Wrong type

            using (var script = new ScriptBuilder
            (
                EVMOpCode.NEWARRAY,
                EVMOpCode.PUSH0,
                EVMOpCode.KEYS
            ))
            using (var engine = CreateEngine(Args))
            {
                // Load script

                engine.LoadScript(script);

                // Execute

                Assert.IsFalse(engine.Execute());

                // Check

                CheckClean(engine, false);
            }
        }

        [TestMethod]
        public void VALUES()
        {
            // Without push

            using (var script = new ScriptBuilder
            (
                EVMOpCode.VALUES
            ))
            using (var engine = CreateEngine(Args))
            {
                // Load script

                engine.LoadScript(script);

                // Execute

                Assert.IsFalse(engine.Execute());

                // Check

                CheckClean(engine, false);
            }

            // Wrong item

            using (var script = new ScriptBuilder
            (
                EVMOpCode.PUSH2,
                EVMOpCode.VALUES
            ))
            using (var engine = CreateEngine(Args))
            {
                // Load script

                engine.LoadScript(script);

                // Execute

                Assert.IsFalse(engine.Execute());

                // Check

                CheckClean(engine, false);
            }

            // Real test - Test clone

            foreach (var isStruct in new bool[] { true, false })
                using (var script = new ScriptBuilder
                (
                    // a= new Array[]{}

                    EVMOpCode.PUSH0,
                    isStruct ? EVMOpCode.NEWSTRUCT : EVMOpCode.NEWARRAY,
                    EVMOpCode.TOALTSTACK,

                    // a.Append(0x01)

                    EVMOpCode.DUPFROMALTSTACK,
                    EVMOpCode.PUSH1,
                    EVMOpCode.APPEND,

                    // a.Append(0x02)

                    EVMOpCode.DUPFROMALTSTACK,
                    EVMOpCode.PUSH2,
                    EVMOpCode.APPEND,

                    // b=a.ToArray()

                    EVMOpCode.DUPFROMALTSTACK,
                    EVMOpCode.VALUES,
                    EVMOpCode.DUP,

                    // b.RemoveAt(0x00)

                    EVMOpCode.PUSH0,
                    EVMOpCode.REMOVE,
                    EVMOpCode.FROMALTSTACK,

                    EVMOpCode.RET
                ))
                using (var engine = CreateEngine(Args))
                {
                    // Load script

                    engine.LoadScript(script);

                    // Execute

                    Assert.IsTrue(engine.Execute());

                    // Check

                    CheckArrayPop(engine.ResultStack, isStruct, 0x01, 0x02);
                    CheckArrayPop(engine.ResultStack, isStruct, 0x02);

                    CheckClean(engine);
                }
        }
    }
}