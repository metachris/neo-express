using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;

namespace NeoExpress.Commands
{
    [Command("export")]
    [Subcommand(typeof(Blocks))]
    internal class ExportCommand
    {
        [Option]
        private string Input { get; } = string.Empty;

        private int OnExecute(CommandLineApplication app, IConsole console)
        {
            try
            {
                var (chain, _) = Program.LoadExpressChain(Input);
                var password = Prompt.GetPassword("Input password to use for exported wallets");

                BlockchainOperations.ExportBlockchain(chain, Directory.GetCurrentDirectory(), password, msg => console.WriteLine(msg));

                return 0;
            }
            catch (Exception ex)
            {
                console.WriteError(ex.Message);
                app.ShowHelp();
                return 1;
            }
        }

        [Command("blocks")]
        private class Blocks
        {

            [Option]
            private string Input { get; } = string.Empty;

            [Argument(0)]
            private int? NodeIndex { get; }

            private int OnExecute(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, _) = Program.LoadExpressChain(Input);
                    var index = NodeIndex.GetValueOrDefault();

                    if (!NodeIndex.HasValue && chain.ConsensusNodes.Count > 1)
                    {
                        throw new Exception("Node index not specified");
                    }

                    if (index >= chain.ConsensusNodes.Count || index < 0)
                    {
                        throw new Exception("Invalid node index");
                    }

                    var node = chain.ConsensusNodes[index];
                    var folder = node.GetBlockchainPath();

                    if (!Directory.Exists(folder))
                    {
                        throw new Exception("cannot export empty blockchain");
                    }

                    BlockchainOperations.ExportBlocks(folder);
                    return 0;
                }
                catch (Exception ex)
                {
                    console.WriteError(ex.Message);
                    app.ShowHelp();
                    return 1;
                }
            }
        }
    }
}
