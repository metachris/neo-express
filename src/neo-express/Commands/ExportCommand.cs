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

            [Option]
            private string Output { get; } = string.Empty;

            [Option]
            private bool Compress { get; }

            [Option]
            private bool Force { get; }


            [Argument(0)]
            private int? NodeIndex { get; }

            private int OnExecute(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var output = string.IsNullOrEmpty(Output)
                       ? Path.Combine(Directory.GetCurrentDirectory(), Compress ? $"chain.acc.zip" : "chain.acc")
                       : Output;

                    if (File.Exists(output))
                    {
                        if (Force)
                        {
                            File.Delete(output);
                        }
                        else
                        {
                            throw new Exception("You must specify force to overwrite exported blocks.");
                        }
                    }

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

                    BlockchainOperations.ExportBlocks(folder, output, Compress);
                    console.WriteLine($"Exported blocks to {output}");

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
