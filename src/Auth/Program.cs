﻿using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using BlubLib;
using BlubLib.Threading.Tasks;
using Dapper;
using Dapper.FastCrud;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using Netsphere.API;
using Netsphere.Network;
using Newtonsoft.Json;
using ProudNet;
using Serilog;
using Serilog.Core;
using Serilog.Formatting.Json;
using System.Threading;

namespace Netsphere
{
    internal class Program
    {
        private static IEventLoopGroup s_apiEventLoopGroup;
        private static IChannel s_apiHost;
        private static IChannel s_loginapiHost;

        private static void Main()
        {
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                Converters = new List<JsonConverter> { new IPEndPointConverter() }
            };
            
            var jsonlog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "auth.json");
            var logfile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "auth.log");
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(new JsonFormatter(), jsonlog)
                .WriteTo.File(logfile)
                .WriteTo.Console(outputTemplate: "[{Level} {SourceContext}] {Message}{NewLine}{Exception}")
                .MinimumLevel.Verbose()
                .CreateLogger();

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            AuthDatabase.Initialize();

            Log.Information("Starting server...");

            AuthServer.Initialize(new Configuration());
            AuthServer.Instance.Listen(Config.Instance.Listener);
            
            s_apiEventLoopGroup = new MultithreadEventLoopGroup(2);
            s_loginapiHost = new ServerBootstrap()
                .Group(s_apiEventLoopGroup)
                .Channel<TcpServerSocketChannel>()
                .Handler(new ActionChannelInitializer<IChannel>(ch => { }))
                .ChildHandler(new ActionChannelInitializer<IChannel>(ch =>
                {
                    ch.Pipeline.AddLast(new LoginServerHandler());
                }))
                .BindAsync(Config.Instance.AuthAPI.Listener).WaitEx();

            s_apiHost = new ServerBootstrap()
                .Group(s_apiEventLoopGroup)
                .Channel<TcpServerSocketChannel>()
                .Handler(new ActionChannelInitializer<IChannel>(ch => { }))
                .ChildHandler(new ActionChannelInitializer<IChannel>(ch =>
                {
                    ch.Pipeline.AddLast(new APIServerHandler());
                }))
                .BindAsync(Config.Instance.API.Listener).WaitEx();

            Log.Information("Ready for connections!");

            if (Config.Instance.NoobMode)
                Log.Warning("!!! NOOB MODE IS ENABLED! EVERY LOGIN SUCCEEDS AND OVERRIDES ACCOUNT LOGIN DETAILS !!!");

            Console.CancelKeyPress += OnCancelKeyPress;
            while (true)
            {
                var input = Console.ReadLine();
                if (input == null)
                    break;

                if (input.Equals("exit", StringComparison.InvariantCultureIgnoreCase) ||
                    input.Equals("quit", StringComparison.InvariantCultureIgnoreCase) ||
                    input.Equals("stop", StringComparison.InvariantCultureIgnoreCase))
                    break;
            }

            Exit();
        }

        private static void OnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Exit();
        }

        private static void Exit()
        {
            Log.Information("Closing...");
            s_apiHost.CloseAsync().WaitEx();
            s_apiEventLoopGroup.ShutdownGracefullyAsync().WaitEx();
            AuthServer.Instance.Dispose();
        }

        private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Log.Error(e.Exception, "UnobservedTaskException");
        }

        private static void OnUnhandledException(object s, UnhandledExceptionEventArgs e)
        {
            Log.Error((Exception)e.ExceptionObject, "UnhandledException");
        }
    }

    internal static class AuthDatabase
    {
        // ReSharper disable once InconsistentNaming
        private static readonly ILogger Logger = Log.ForContext(Constants.SourceContextPropertyName, nameof(AuthDatabase));
        private static string s_connectionString;

        public static void Initialize()
        {
            Logger.Information("Initializing...");

            var config = Config.Instance.Database;

            switch (config.Engine)
            {
                case DatabaseEngine.MySQL:
                    s_connectionString = $"SslMode=none;Server={config.Auth.Host};Port={config.Auth.Port};Database={config.Auth.Database};Uid={config.Auth.Username};Pwd={config.Auth.Password};Pooling=true;";
                    OrmConfiguration.DefaultDialect = SqlDialect.MySql;

                    using (var con = Open())
                    {
                        if (con.QueryFirstOrDefault($"SHOW DATABASES LIKE \"{config.Auth.Database}\"") == null)
                        {
                            Logger.Error($"Database '{config.Auth.Database}' not found");
                            Environment.Exit(0);
                        }
                    }
                    break;

                case DatabaseEngine.SQLite:
                    s_connectionString = $"Data Source={config.Auth.Filename};Pooling=true;";
                    OrmConfiguration.DefaultDialect = SqlDialect.SqLite;

                    if (!File.Exists(config.Auth.Filename))
                    {
                        Logger.Error($"Database '{config.Auth.Filename}' not found");
                        Environment.Exit(0);
                    }
                    break;

                default:
                    Logger.Error($"Invalid database engine {config.Engine}");
                    Environment.Exit(0);
                    return;
            }
        }

        public static IDbConnection Open()
        {
            var engine = Config.Instance.Database.Engine;
            IDbConnection connection;
            switch (engine)
            {
                case DatabaseEngine.MySQL:
                    connection = new MySql.Data.MySqlClient.MySqlConnection(s_connectionString);
                    break;

                case DatabaseEngine.SQLite:
                    connection = new Microsoft.Data.Sqlite.SqliteConnection(s_connectionString);
                    break;

                default:
                    Logger.Error($"Invalid database engine {engine}");
                    Environment.Exit(0);
                    return null;
            }
            connection.Open();
            return connection;
        }
    }
}
