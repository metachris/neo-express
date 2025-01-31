using System;
using System.Collections.Generic;
using Neo;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;

namespace NeoExpress.Node
{
    internal class ExpressApplicationEngine : ApplicationEngine
    {
        private readonly ITraceDebugSink traceDebugSink;
        private readonly Dictionary<UInt160, string> contractNameMap = new Dictionary<UInt160, string>();

        public ExpressApplicationEngine(ITraceDebugSink traceDebugSink, TriggerType trigger, IVerifiable container,
                                        DataCache snapshot, Block? persistingBlock, ProtocolSettings settings, long gas)
            : base(trigger, container, snapshot, persistingBlock, settings, gas)
        {
            this.traceDebugSink = traceDebugSink;

            Log += OnLog!;
            Notify += OnNotify!;
        }

        public override void Dispose()
        {
            Log -= OnLog!;
            Notify -= OnNotify!;
            traceDebugSink.Dispose();
            base.Dispose();
        }

        private string GetContractName(UInt160 scriptId)
        {
            if (contractNameMap.TryGetValue(scriptId, out var name))
            {
                return name;
            }

            var state = NativeContract.ContractManagement.GetContract(Snapshot, scriptId);
            name = state != null ? state.Manifest.Name : "";
            contractNameMap[scriptId] = name;
            return name;
        }

        private void OnNotify(object sender, NotifyEventArgs args)
        {
            if (ReferenceEquals(sender, this))
            {
                var name = GetContractName(args.ScriptHash);
                traceDebugSink.Notify(args, name);
            }
        }

        private void OnLog(object sender, LogEventArgs args)
        {
            if (ReferenceEquals(sender, this))
            {
                var name = GetContractName(args.ScriptHash);
                traceDebugSink.Log(args, name);
            }
        }

        public override VMState Execute()
        {
            traceDebugSink.Script(CurrentContext?.Script ?? Array.Empty<byte>());
            traceDebugSink.Trace(State, InvocationStack);
            WriteStorages(CurrentScriptHash);

            return base.Execute();
        }

        protected override void PostExecuteInstruction()
        {
            base.PostExecuteInstruction();

            if (State == VMState.HALT)
            {
                traceDebugSink.Results(State, GasConsumed, ResultStack);
            }
            traceDebugSink.Trace(State, InvocationStack);
            WriteStorages(CurrentScriptHash);
        }

        protected override void OnFault(Exception e)
        {
            base.OnFault(e);
            traceDebugSink.Fault(e);
            traceDebugSink.Trace(State, InvocationStack);
        }

        private void WriteStorages(UInt160 scriptHash)
        {
            if (scriptHash != null)
            {
                var contractState = NativeContract.ContractManagement.GetContract(Snapshot, scriptHash);
                if (contractState != null)
                {
                    var storages = Snapshot.Find(StorageKey.CreateSearchPrefix(contractState.Id, default));
                    traceDebugSink.Storages(scriptHash, storages);
                }
            }
        }
    }
}
