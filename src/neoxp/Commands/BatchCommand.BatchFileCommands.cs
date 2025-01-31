using System;
using System.ComponentModel.DataAnnotations;
using McMaster.Extensions.CommandLineUtils;
using Neo.Network.P2P.Payloads;

namespace NeoExpress.Commands
{
    partial class BatchCommand
    {
        [Command]
        [Subcommand(typeof(Checkpoint), typeof(Contract), typeof(Oracle), typeof(Transfer))]
        internal class BatchFileCommands
        {
            [Command("transfer")]
            internal class Transfer
            {
                [Argument(0, Description = "Amount to transfer")]
                [Required]
                internal string Quantity { get; init; } = string.Empty;

                [Argument(1, Description = "Asset to transfer (symbol or script hash)")]
                [Required]
                internal string Asset { get; init; } = string.Empty;

                [Argument(2, Description = "Account to send asset from")]
                [Required]
                internal string Sender { get; init; } = string.Empty;

                [Argument(3, Description = "Account to send asset to")]
                [Required]
                internal string Receiver { get; init; } = string.Empty;
            }

            [Command("checkpoint")]
            [Subcommand(typeof(Create))]
            internal class Checkpoint
            {
                [Command("create")]
                internal class Create
                {
                    [Argument(0, "Checkpoint file name")]
                    [Required]
                    internal string Name { get; init; } = string.Empty;

                    [Option(Description = "Overwrite existing data")]
                    internal bool Force { get; }
                }
            }

            [Command("contract")]
            [Subcommand(typeof(Deploy), typeof(Invoke))]
            internal class Contract
            {
                [Command("deploy")]
                internal class Deploy
                {
                    [Argument(0, Description = "Path to contract .nef file")]
                    [Required]
                    internal string Contract { get; init; } = string.Empty;

                    [Argument(1, Description = "Account to pay contract deployment GAS fee")]
                    [Required]
                    internal string Account { get; init; } = string.Empty;

                    [Option(Description = "Witness Scope to use for transaction signer (Default: CalledByEntry)")]
                    [AllowedValues(StringComparison.OrdinalIgnoreCase, "None", "CalledByEntry", "Global")]
                    internal WitnessScope WitnessScope { get; init; } = WitnessScope.CalledByEntry;
                }

                [Command("invoke")]
                internal class Invoke
                {
                    [Argument(0, Description = "Path to contract invocation JSON file")]
                    [Required]
                    internal string InvocationFile { get; init; } = string.Empty;

                    [Argument(1, Description = "Account to pay contract invocation GAS fee")]
                    [Required]
                    internal string Account { get; init; } = string.Empty;

                    [Option(Description = "Witness Scope to use for transaction signer (Default: CalledByEntry)")]
                    [AllowedValues(StringComparison.OrdinalIgnoreCase, "None", "CalledByEntry", "Global")]
                    internal WitnessScope WitnessScope { get; init; } = WitnessScope.CalledByEntry;
                }
            }

            [Command("oracle")]
            [Subcommand(typeof(Enable), typeof(Response))]
            internal class Oracle
            {
                [Command("enable")]
                internal class Enable
                {
                    [Argument(0, Description = "Account to pay contract invocation GAS fee")]
                    [Required]
                    internal string Account { get; init; } = string.Empty;
                }

                [Command("response")]
                internal class Response
                {
                    [Argument(0, Description = "URL of oracle request")]
                    [Required]
                    internal string Url { get; init; } = string.Empty;

                    [Argument(1, Description = "Path to JSON file with oracle response cotnent")]
                    [Required]
                    internal string ResponsePath { get; init; } = string.Empty;
                }
            }
        }
    }
}
