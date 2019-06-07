using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ADTest
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length <= 1
                || args[0] == "help"
                || args[0] == "-help"
                || args[0] == "--h"
                || args[0] == "/?")
            {
                Console.WriteLine("-users SEARCH");
                Console.WriteLine("-userdetails LOGINNAME");
                Console.WriteLine("-group GROUP");
                Console.WriteLine("-usersingroup GROUP");

                return;
            }

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            var serviceProvider = serviceCollection.BuildServiceProvider();

            var activeDirectoryUserService = serviceProvider.GetService<IActiveDirectoryUserService>();

            if (args[0] == "-users")
            {
                var users = activeDirectoryUserService.FindMatchingUsers(args[1]);

                if (!users.Any())
                {
                    Console.WriteLine("No users found.");
                }
                else
                {
                    foreach (var user in users)
                    {
                        Console.WriteLine($"Name: {user.DisplayName}\tE-Mail: {user.Email}\tLoginName: {user.LoginName}");
                    }
                }
            }
            else if (args[0] == "-userdetails")
            {
                var user = activeDirectoryUserService.FindByLogin(args[1]);

                if (user == null)
                {
                    Console.WriteLine("No user found.");
                }
                else
                {
                    Console.WriteLine($"Name: {user.DisplayName}");
                    Console.WriteLine($"First name: {user.FirstName}");
                    Console.WriteLine($"Last name: {user.LastName}");
                    Console.WriteLine($"E-Mail: {user.Email}");
                    Console.WriteLine($"LoginName: {user.LoginName}");
                    Console.WriteLine("Groups:");

                    foreach (var group in user.Groups)
                    {
                        Console.WriteLine($"\t{group}");
                    }
                }
            }
            else if (args[0] == "-group")
            {
                var groups = activeDirectoryUserService.GetGroupWithChildGroups(args[1]);

                if (!groups.Any())
                {
                    Console.WriteLine("No group found.");
                }
                else
                {
                    foreach (var group in groups)
                    {
                        Console.WriteLine(group);
                    }
                }
            }
            else if (args[0] == "-usersingroup")
            {
                var users = activeDirectoryUserService.FindMatchingUsersByGroup(args[1]);

                if (!users.Any())
                {
                    Console.WriteLine("No users found.");
                }
                else
                {
                    foreach (var user in users)
                    {
                        Console.WriteLine($"Name: {user.DisplayName}\tE-Mail: {user.Email}\tLoginName: {user.LoginName}");
                    }
                }
            }
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            var configuration = builder.Build();

            services.AddLogging(configure => configure
                .AddConsole());

            services.Configure<ActiveDirectorySettings>(configuration.GetSection("ActiveDirectorySettings"));
            services.AddTransient<IActiveDirectoryUserService, ActiveDirectoryUserService>();
        }
    }
}
