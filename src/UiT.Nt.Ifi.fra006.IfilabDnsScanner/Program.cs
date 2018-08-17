using McMaster.Extensions.CommandLineUtils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;

namespace UiT.Nt.Ifi.fra006.IfilabDnsScanner
{
    public static class Program
    {
        private static readonly TypeInfo ProgramTypeInfo = typeof(Program).GetTypeInfo();

        public static int Main(string[] args)
        {
            var application = new CommandLineApplication()
            {
                Name = nameof(IfilabDnsScanner),
                FullName = ProgramTypeInfo.Assembly.GetName().Name,
                Description = ProgramTypeInfo.Assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description
            };
            application.HelpOption("-?|-h|--help");
            application.VersionOption("-v|--version", () =>
            {
                var informationVersionAttr = ProgramTypeInfo.Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                if (string.IsNullOrWhiteSpace(informationVersionAttr?.InformationalVersion))
                    return ProgramTypeInfo.Assembly.GetName().Version.ToString();
                return informationVersionAttr.InformationalVersion;
            }, () =>
            {
                var informationVersionAttr = ProgramTypeInfo.Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                if (string.IsNullOrWhiteSpace(informationVersionAttr?.InformationalVersion))
                    return ProgramTypeInfo.Assembly.GetName().Version.ToString();
                return informationVersionAttr.InformationalVersion + " (" + ProgramTypeInfo.Assembly.GetName().Version + ")";
            });

            const string hostFormatDefault = "ifilab{0}.stud.cs.uit.no";
            var hostFormatOption = application.Option("-f|--format <HOSTFORMAT>", ".NET Format String for the hostnames to lookup. Default: " + hostFormatDefault, CommandOptionType.SingleValue);

            const int hostParamLowerDefault = byte.MinValue;
            var hostParamLowerOption = application.Option("-l|--lower <LOWER>", "Lower inclusive integer bound to apply to the host format. Default: " + hostParamLowerDefault.ToString(CultureInfo.CurrentCulture), CommandOptionType.SingleValue);

            const int hostParamUpperDefault = byte.MaxValue;
            var hostParamUpperOption = application.Option("-u|--upper <UPPER>", "Upper exclusive integer bound to apply to the host format. Default: " + hostParamUpperDefault.ToString(CultureInfo.CurrentCulture), CommandOptionType.SingleValue);

            var hostTcpPortListDefault = Enumerable.Empty<int>();
            var hostTcpPortListOption = application.Option("-t|--tcp <PORTS>", "A list of TCP port numbers to attempt to open a TCP connection to. The current culture is used to acertain the list separator.", CommandOptionType.SingleValue);

            var hostTcpPortReqdOption = application.Option("--tcp-reqd", "Causes hosts that do not listen on all specified ports to be classified as failures.", CommandOptionType.NoValue);

            var hostFailuresOption = application.Option("--show-failures", "Also includes failed lookups and connection attempts in the output.", CommandOptionType.NoValue);
            var hostAliasesOmitOption = application.Option("--no-alias", "Omits the host aliases in the output.", CommandOptionType.NoValue);
            var hostAddressListOmitOption = application.Option("--no-address", "Omits the host IP address list in the output.", CommandOptionType.NoValue);
            var hostPortsOmitOption = application.Option("--no-port", "Omits the host's verified open TCP ports in the output.", CommandOptionType.NoValue);

            var cultureInvariantOption = application.Option("--culture-invariant", "Uses an invariant culture to parse and display values. Default: OS-decided culture", CommandOptionType.NoValue);
            var cultureSpecificOption = application.Option("--culture-specific <CULTURE>", "Uses the cultured by the specified ISO culture name to parse and display values. Default: OS-decided culture", CommandOptionType.SingleValue);

            application.OnExecute(async () =>
            {
                if (cultureInvariantOption.HasValue())
                {
#if (NET451 || NET452)
                    System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                    System.Threading.Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
#else // !(NET451 || NET452)
                    CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                    CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
#endif // !(NET451 || NET452)
                }
                else if (cultureSpecificOption.HasValue())
                {
                    var specificCulture = new CultureInfo(cultureSpecificOption.Value());
#if (NET451 || NET452)
                    System.Threading.Thread.CurrentThread.CurrentCulture = specificCulture;
                    System.Threading.Thread.CurrentThread.CurrentUICulture = specificCulture;
#else // !(NET451 || NET452)
                    CultureInfo.CurrentCulture = specificCulture;
                    CultureInfo.CurrentUICulture = specificCulture;
#endif // !(NET451 || NET452)
                }

                string hostFormatString;
                if (hostFormatOption.HasValue())
                    hostFormatString = hostFormatOption.Value();
                else
                    hostFormatString = hostFormatDefault;

                int hostParamLowerValue;
                if (hostParamLowerOption.HasValue())
                {
                    if (!int.TryParse(hostParamLowerOption.Value(), NumberStyles.AllowExponent | NumberStyles.AllowLeadingWhite | NumberStyles.AllowThousands | NumberStyles.AllowTrailingWhite, CultureInfo.CurrentCulture, out hostParamLowerValue))
                    {
                        Console.Error.WriteLine("Invalid option value for option '{0}': {1}", hostParamLowerOption.LongName, hostParamLowerOption.Value());
                        Console.Error.WriteLine("\tSpecified value could not be parsed as a non-negative 32-bit integer value.");
                        return -1;
                    }
                }
                else
                    hostParamLowerValue = hostParamLowerDefault;

                int hostParamUpperValue;
                if (hostParamUpperOption.HasValue())
                {
                    if (!int.TryParse(hostParamUpperOption.Value(), NumberStyles.AllowExponent | NumberStyles.AllowLeadingWhite | NumberStyles.AllowThousands | NumberStyles.AllowTrailingWhite, CultureInfo.CurrentCulture, out hostParamUpperValue))
                    {
                        Console.Error.WriteLine("Invalid option value for option '{0}': {1}", hostParamUpperOption.LongName, hostParamUpperOption.Value());
                        Console.Error.WriteLine("\tSpecified value could not be parsed as a non-negative 32-bit integer value.");
                        return -1;
                    }
                }
                else
                    hostParamUpperValue = hostParamUpperDefault;
                if (hostParamUpperValue < hostParamLowerValue)
                {
                    Console.Error.WriteLine("Invalid option value for option '{0}': {1}", hostParamUpperOption.LongName, hostParamUpperOption.Value());
                    Console.Error.WriteLine("\tSpecified value is less than {0}, the value use for the lower inclusive bound.");
                    return -2;
                }

                IEnumerable<int> hostTcpPortListValue;
                if (hostTcpPortListOption.HasValue())
                {
                    var hostTcpPortSplitStrings = hostTcpPortListOption.Value().Split(new string[] { CultureInfo.CurrentCulture.TextInfo?.ListSeparator ?? CultureInfo.InvariantCulture.TextInfo.ListSeparator }, StringSplitOptions.RemoveEmptyEntries);
                    var hostTcpPortArray = new List<int>(hostTcpPortSplitStrings.Length);
                    foreach (var hostTcpPortString in hostTcpPortSplitStrings)
                    {
                        if (!int.TryParse(hostTcpPortString, NumberStyles.AllowLeadingWhite | NumberStyles.AllowThousands | NumberStyles.AllowTrailingWhite, CultureInfo.CurrentCulture, out int hostTcpPortValue))
                        {
                            Console.WriteLine("Invalid TCP port number specified: {0}", hostTcpPortString);
                            Console.WriteLine("\tThe specified value could not be parsed as a non-negative 32-bit integer value.");
                            continue;
                        }
                        if (hostTcpPortValue < IPEndPoint.MinPort)
                        {
                            Console.WriteLine("Invalid TCP port number specified: {0}", hostTcpPortValue);
                            Console.WriteLine("\tThe specified value is less than the minimum allowed port value: {0}", IPEndPoint.MinPort);
                            Console.WriteLine("\tThe specified value will be ignored");
                            continue;
                        }
                        if (hostTcpPortValue > IPEndPoint.MaxPort)
                        {
                            Console.WriteLine("Invalid TCP port number specified: {0}", hostTcpPortValue);
                            Console.WriteLine("\tThe specified value is greater than the maximum allowed port value: {0}", IPEndPoint.MaxPort);
                            Console.WriteLine("\tThe specified value will be ignored");
                            continue;
                        }
                        hostTcpPortArray.Add(hostTcpPortValue);
                    }
                    hostTcpPortListValue = hostTcpPortArray;
                }
                else
                    hostTcpPortListValue = hostTcpPortListDefault;

                var hostEntryTaskTupleList = Enumerable.Range(hostParamLowerValue, hostParamUpperValue - hostParamLowerValue)
                    .Select(i => string.Format(hostFormatString, i.ToString(CultureInfo.CurrentCulture)))
                    .Select(async hostname =>
                    {
                        IPHostEntry hostentry;
                        try { hostentry = await Dns.GetHostEntryAsync(hostname); }
                        catch (SocketException socketExcept)
                        { return Tuple.Create<string, IPHostEntry, SocketException, List<Task<Tuple<int, SocketException>>>>(hostname, null, socketExcept, null); }
                        return Tuple.Create(hostname, hostentry, (SocketException)null, hostTcpPortListValue.Select(async p =>
                        {
                            using (var hostTcpClient = new TcpClient())
                            {
                                try { await hostTcpClient.ConnectAsync(hostentry.AddressList, p); }
                                catch (SocketException socketExcept) { return Tuple.Create(p, socketExcept); }
                                return Tuple.Create<int, SocketException>(p, null);
                            }
                        }).ToList());
                    }).ToList();
                foreach (var hostEntryTupleTask in hostEntryTaskTupleList)
                {
                    var hostEntryTuple = await hostEntryTupleTask;
                    var hostName = hostEntryTuple.Item1;
                    if (hostEntryTuple.Item3 != null)
                    {
                        if (hostFailuresOption.HasValue())
                        {
                            var socketExcept = hostEntryTuple.Item3;
                            Console.WriteLine("{0}: Socket error: {1}, {2}", hostName, socketExcept.SocketErrorCode, socketExcept.Message);
                        }

                        continue;
                    }

                    IPHostEntry hostEntry = hostEntryTuple.Item2;
                    var hostTcpPortTupleTaskList = hostEntryTuple.Item4;
                    if (!hostTcpPortReqdOption.HasValue() || hostTcpPortTupleTaskList.All(t => t.Result.Item2 == null))
                    {
                        Console.Write(hostEntry.HostName);
                        if (!hostAliasesOmitOption.HasValue() && (hostEntry.Aliases?.Any(alias => !string.Equals(alias, hostEntry.HostName, StringComparison.OrdinalIgnoreCase)) ?? false))
                            Console.Write(" (\"{0}\")", string.Join("\", \"", hostEntry.Aliases));
                        if (!hostAddressListOmitOption.HasValue() && (hostEntry.AddressList?.Any() ?? false))
                            Console.Write(": " + string.Join((CultureInfo.CurrentCulture.TextInfo?.ListSeparator ?? CultureInfo.InvariantCulture.TextInfo.ListSeparator).Trim() + " ", hostEntry.AddressList.Select(a => a.ToString())));
                        var hostTcpPortListenerNumbers = new List<int>(hostTcpPortTupleTaskList.Count);
                        var hostTcpPortConnectErrors = new List<Tuple<int, SocketException>>(hostTcpPortTupleTaskList.Count);
                        foreach (var hostTcpPortTupleTask in hostTcpPortTupleTaskList)
                        {
                            var hostTcpPortTuple = await hostTcpPortTupleTask;
                            if (hostTcpPortTuple.Item2 == null)
                                hostTcpPortListenerNumbers.Add(hostTcpPortTuple.Item1);
                            else
                                hostTcpPortConnectErrors.Add(hostTcpPortTuple);
                        }
                        if (!hostPortsOmitOption.HasValue() && hostTcpPortListenerNumbers.Any())
                            Console.Write(" TCP: " + string.Join((CultureInfo.CurrentCulture.TextInfo?.ListSeparator ?? CultureInfo.InvariantCulture.TextInfo.ListSeparator), hostTcpPortListenerNumbers.Select(p => p.ToString())));
                        if (hostFailuresOption.HasValue())
                        {
                            foreach (var hostTcpConnectError in hostTcpPortConnectErrors)
                            {
                                Console.WriteLine();
                                Console.Write("\tUnable to connect to port {0}: Socket error: {1}: {2}", hostTcpConnectError.Item1, hostTcpConnectError.Item2.SocketErrorCode, hostTcpConnectError.Item2.Message);
                            }
                        }
                        Console.WriteLine();
                    }
                }

                return 0;
            });

            return application.Execute(args);
        }
    }
}
