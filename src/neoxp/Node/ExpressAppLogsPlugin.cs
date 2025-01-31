﻿using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using Neo;
using Neo.IO;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using Neo.VM;
using NeoExpress.Models;
using ApplicationExecuted = Neo.Ledger.Blockchain.ApplicationExecuted;

namespace NeoExpress.Node
{
    internal partial class ExpressAppLogsPlugin : Plugin, IPersistencePlugin
    {
        private const string APP_LOGS_STORE_PATH = "app-logs-store";
        private const string NOTIFICATIONS_STORE_PATH = "notifications-store";

        static IStore GetAppLogStore(IStorageProvider storageProvider) => storageProvider.GetStore(APP_LOGS_STORE_PATH);
        static IStore GetNotificationsStore(IStorageProvider storageProvider) => storageProvider.GetStore(NOTIFICATIONS_STORE_PATH);

        private readonly IStore appLogsStore;
        private readonly IStore notificationsStore;

        public ExpressAppLogsPlugin(IStorageProvider storageProvider)
        {
            appLogsStore = GetAppLogStore(storageProvider);
            notificationsStore = GetNotificationsStore(storageProvider);
        }

        public static JObject? GetAppLog(IStorageProvider storageProvider, UInt256 hash)
        {
            var store = GetAppLogStore(storageProvider);
            var value = store.TryGet(hash.ToArray());
            return value != null && value.Length != 0
                ? JObject.Parse(Neo.Utility.StrictUTF8.GetString(value))
                : null;
        }

        public static IEnumerable<(uint blockIndex, ushort txIndex, NotificationRecord notification)> GetNotifications(IStorageProvider storageProvider)
        {
            var store = GetNotificationsStore(storageProvider);

            return store.Seek(Array.Empty<byte>(), SeekDirection.Forward)
                .Select(t =>
                {
                    var (blockIndex, txIndex) = ParseNotificationKey(t.Key);
                    return (blockIndex, txIndex, t.Value.AsSerializable<NotificationRecord>());
                });

            static (uint, ushort) ParseNotificationKey(byte[] key)
            {
                var blockIndex = BinaryPrimitives.ReadUInt32BigEndian(key.AsSpan(0, sizeof(uint)));
                var txIndex = BinaryPrimitives.ReadUInt16BigEndian(key.AsSpan(sizeof(uint), sizeof(ushort)));
                return (blockIndex, txIndex);
            }
        }

        public void OnPersist(NeoSystem system, Block block, DataCache snapshot, IReadOnlyList<ApplicationExecuted> applicationExecutedList)
        {
            if (applicationExecutedList.Count > ushort.MaxValue) throw new Exception("applicationExecutedList too big");

            var notificationIndex = new byte[sizeof(uint) + (2 * sizeof(ushort))];
            BinaryPrimitives.WriteUInt32BigEndian(
                notificationIndex.AsSpan(0, sizeof(uint)),
                block.Index);

            for (int i = 0; i < applicationExecutedList.Count; i++)
            {
                ApplicationExecuted appExec = applicationExecutedList[i];
                if (appExec.Transaction == null) continue;

                var txJson = TxLogToJson(appExec);
                appLogsStore.Put(appExec.Transaction.Hash.ToArray(), Neo.Utility.StrictUTF8.GetBytes(txJson.ToString()));

                if (appExec.VMState != VMState.FAULT)
                {
                    if (appExec.Notifications.Length > ushort.MaxValue) throw new Exception("appExec.Notifications too big");

                    BinaryPrimitives.WriteUInt16BigEndian(notificationIndex.AsSpan(sizeof(uint), sizeof(ushort)), (ushort)i);

                    for (int j = 0; j < appExec.Notifications.Length; j++)
                    {
                        BinaryPrimitives.WriteUInt16BigEndian(
                            notificationIndex.AsSpan(sizeof(uint) + sizeof(ushort), sizeof(ushort)),
                            (ushort)j);
                        var record = new NotificationRecord(appExec.Notifications[j]);
                        notificationsStore.Put(notificationIndex.ToArray(), record.ToArray());
                    }
                }
            }

            var blockJson = BlockLogToJson(block, applicationExecutedList);
            if (blockJson != null)
            {
                appLogsStore.Put(block.Hash.ToArray(), Neo.Utility.StrictUTF8.GetBytes(blockJson.ToString()));
            }
        }

        // TxLogToJson and BlockLogToJson copied from Neo.Plugins.LogReader in the ApplicationLogs plugin
        // to avoid dependency on LevelDBStore package

        public static JObject TxLogToJson(Blockchain.ApplicationExecuted appExec)
        {
            global::System.Diagnostics.Debug.Assert(appExec.Transaction != null);

            var txJson = new JObject();
            txJson["txid"] = appExec.Transaction.Hash.ToString();
            JObject trigger = new JObject();
            trigger["trigger"] = appExec.Trigger;
            trigger["vmstate"] = appExec.VMState;
            trigger["exception"] = GetExceptionMessage(appExec.Exception);
            trigger["gasconsumed"] = appExec.GasConsumed.ToString();
            try
            {
                trigger["stack"] = appExec.Stack.Select(q => q.ToJson()).ToArray();
            }
            catch (InvalidOperationException)
            {
                trigger["stack"] = "error: recursive reference";
            }
            trigger["notifications"] = appExec.Notifications.Select(q =>
            {
                JObject notification = new JObject();
                notification["contract"] = q.ScriptHash.ToString();
                notification["eventname"] = q.EventName;
                try
                {
                    notification["state"] = q.State.ToJson();
                }
                catch (InvalidOperationException)
                {
                    notification["state"] = "error: recursive reference";
                }
                return notification;
            }).ToArray();

            txJson["executions"] = new List<JObject>() { trigger }.ToArray();
            return txJson;
        }

        public static JObject? BlockLogToJson(Block block, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            var blocks = applicationExecutedList.Where(p => p.Transaction is null).ToArray();
            if (blocks.Length > 0)
            {
                var blockJson = new JObject();
                var blockHash = block.Hash.ToArray();
                blockJson["blockhash"] = block.Hash.ToString();
                var triggerList = new List<JObject>();
                foreach (var appExec in blocks)
                {
                    JObject trigger = new JObject();
                    trigger["trigger"] = appExec.Trigger;
                    trigger["vmstate"] = appExec.VMState;
                    trigger["gasconsumed"] = appExec.GasConsumed.ToString();
                    try
                    {
                        trigger["stack"] = appExec.Stack.Select(q => q.ToJson()).ToArray();
                    }
                    catch (InvalidOperationException)
                    {
                        trigger["stack"] = "error: recursive reference";
                    }
                    trigger["notifications"] = appExec.Notifications.Select(q =>
                    {
                        JObject notification = new JObject();
                        notification["contract"] = q.ScriptHash.ToString();
                        notification["eventname"] = q.EventName;
                        try
                        {
                            notification["state"] = q.State.ToJson();
                        }
                        catch (InvalidOperationException)
                        {
                            notification["state"] = "error: recursive reference";
                        }
                        return notification;
                    }).ToArray();
                    triggerList.Add(trigger);
                }
                blockJson["executions"] = triggerList.ToArray();
                return blockJson;
            }

            return null;
        }

        static string? GetExceptionMessage(Exception exception)
        {
            return exception?.GetBaseException().Message;
        }
    }
}
