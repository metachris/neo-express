using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;


namespace NeoExpress.Commands
{
    partial class CheckpointCommand
    {
        [Command("run", Description = "Run a neo-express checkpoint (discarding changes on shutdown)")]
        internal class Run
        {
            readonly IExpressChainManagerFactory chainManagerFactory;

            public Run(IExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }

            [Argument(0, "Checkpoint file name")]
            [Required]
            internal string Name { get; init; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            [Option(Description = "Time between blocks")]
            internal uint SecondsPerBlock { get; }

            [Option(Description = "Enable contract execution tracing")]
            internal bool Trace { get; init; } = false;

            internal async Task ExecuteAsync(IConsole console, CancellationToken token)
            {
                var (chainManager, _) = chainManagerFactory.LoadChain(Input, SecondsPerBlock);
                var chain = chainManager.Chain;
                if (chain.ConsensusNodes.Count != 1)
                {
                    throw new ArgumentException("Checkpoint create is only supported on single node express instances", nameof(chain));
                }

                using var storageProvider = chainManager.GetCheckpointStorageProvider(Name);
                await chainManager.RunAsync(storageProvider, chain.ConsensusNodes[0], Trace, console.Out, token);
            }

            internal async Task<int> OnExecuteAsync(IConsole console, CancellationToken token)
            {
                try
                {
                    await ExecuteAsync(console, token).ConfigureAwait(false);
                    return 0;
                }
                catch (Exception ex)
                {
                    await console.Error.WriteLineAsync(ex.Message);
                    return 1;
                }
            }
        }
    }
}
