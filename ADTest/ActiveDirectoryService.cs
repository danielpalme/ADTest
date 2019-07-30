using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Security.Principal;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ADTest
{
    public class ActiveDirectoryService : IActiveDirectoryService
    {
        private const int MaximumItems = 21; // 20 items are displayed at once, but 21 are retrieved to determine if more hits do exist

        private readonly ILogger logger;

        private readonly ActiveDirectorySettings activeDirectorySettings;

        public ActiveDirectoryService(ILogger<ActiveDirectoryService> logger, IOptions<ActiveDirectorySettings> optionsAccessor)
        {
            this.logger = logger;
            this.activeDirectorySettings = optionsAccessor.Value;
        }

        public ActiveDirectoryUser GetByLogin(string loginName)
        {
            if (loginName == null)
            {
                throw new ArgumentNullException(nameof(loginName));
            }

            loginName = loginName.ToLowerInvariant();

            int indexOfBackslash = loginName.IndexOf("\\");
            string samaccountname = EscapeLdapSearchFilter(loginName.Substring(indexOfBackslash + 1));

            using (DirectorySearcher directorySearcher = this.CreateDirectorySearcher())
            {
                ConfigureDirectorySearcher(directorySearcher);

                directorySearcher.Filter = "(&(objectCategory=Person)(objectClass=User)(!userAccountControl:1.2.840.113556.1.4.803:=2)(samaccountname=" + samaccountname + "))";

                try
                {
                    foreach (SearchResult principal in directorySearcher.FindAll())
                    {
                        using (DirectoryEntry directoryEntry = principal.GetDirectoryEntry())
                        {
                            var user = this.Convert(directoryEntry, true, true);

                            if (user.LoginName == loginName)
                            {
                                return user;
                            }
                        }
                    }
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                }
            }

            return null;
        }

        public IEnumerable<ActiveDirectoryUser> FindMatchingUsers(string filterText)
        {
            List<ActiveDirectoryUser> users = new List<ActiveDirectoryUser>();

            if (filterText == null || filterText.Length < 3)
            {
                return users;
            }

            filterText = EscapeLdapSearchFilter(filterText);

            using (DirectorySearcher directorySearcher = this.CreateDirectorySearcher())
            {
                directorySearcher.Sort = new SortOption("name", SortDirection.Ascending);

                ConfigureDirectorySearcher(directorySearcher);
                directorySearcher.Filter = "(&(&(objectCategory=Person)(objectClass=User)(!userAccountControl:1.2.840.113556.1.4.803:=2))(|(name=" + filterText + "*)(sn=" + filterText + "*)(givenName=" + filterText + "*)(mail=" + filterText + "*)))";

                try
                {
                    foreach (SearchResult principal in directorySearcher.FindAll())
                    {
                        using (DirectoryEntry directoryEntry = principal.GetDirectoryEntry())
                        {
                            var activeDirectoryUser = this.Convert(directoryEntry, false, false);

                            if (activeDirectoryUser.Email == null)
                            {
                                continue;
                            }

                            users.Add(activeDirectoryUser);
                        }

                        if (users.Count >= MaximumItems)
                        {
                            break;
                        }
                    }
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                }
            }

            return users.OrderBy(u => u.DisplayName);
        }

        public IEnumerable<ActiveDirectoryUser> FindMatchingUsersByGroup(string groupName)
        {
            if (groupName == null)
            {
                throw new ArgumentNullException(nameof(groupName));
            }

            var groups = new HashSet<ActiveDirectoryGroup>();
            this.AddGroups(groups, groupName, false, true, true);

            List<ActiveDirectoryUser> users = new List<ActiveDirectoryUser>();

            if (groups.Count == 0)
            {
                return users;
            }

            string groupsQuery = $"(memberof:1.2.840.113556.1.4.1941:={groups.First().DistinguishedName})";

            this.logger.LogInformation($"QUERY: {groupsQuery}");

            using (DirectorySearcher directorySearcher = this.CreateDirectorySearcher())
            {
                directorySearcher.Sort = new SortOption("name", SortDirection.Ascending);

                ConfigureDirectorySearcher(directorySearcher);
                // See: https://ldapwiki.com/wiki/Active%20Directory%20Group%20Related%20Searches
                directorySearcher.Filter = $"(&(objectCategory=Person)(objectClass=User)(!userAccountControl:1.2.840.113556.1.4.803:=2){groupsQuery})";

                try
                {
                    foreach (SearchResult principal in directorySearcher.FindAll())
                    {
                        using (DirectoryEntry directoryEntry = principal.GetDirectoryEntry())
                        {
                            var activeDirectoryUser = this.Convert(directoryEntry, false, false);

                            if (activeDirectoryUser.Email == null)
                            {
                                continue;
                            }

                            users.Add(activeDirectoryUser);
                        }
                    }
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                }
            }

            return users;
        }

        public IEnumerable<ActiveDirectoryGroup> FindMatchingGroups(string filterText)
        {
            HashSet<ActiveDirectoryGroup> groups = new HashSet<ActiveDirectoryGroup>();

            if (filterText == null || filterText.Length < 3)
            {
                return groups;
            }

            int indexOfBackslash = filterText.IndexOf("\\");
            filterText = "*" + EscapeLdapSearchFilter(filterText.Substring(indexOfBackslash + 1)) + "*";

            this.AddGroups(groups, filterText, false, true, true);

            return groups;
        }

        public IEnumerable<ActiveDirectoryGroup> GetGroups(string groupName, bool recursice)
        {
            int indexOfBackslash = groupName.IndexOf("\\");
            groupName = EscapeLdapSearchFilter(groupName.Substring(indexOfBackslash + 1));

            HashSet<ActiveDirectoryGroup> groups = new HashSet<ActiveDirectoryGroup>();
            this.AddGroups(groups, groupName, recursice, false, true);

            return groups;
        }

        private void AddGroups(HashSet<ActiveDirectoryGroup> groups, string groupName, bool recursive, bool limit, bool includeDomain)
        {
            using (DirectorySearcher directorySearcher = this.CreateDirectorySearcher())
            {
                directorySearcher.PropertiesToLoad.Add("name");
                directorySearcher.PropertiesToLoad.Add("distinguishedName");

                if (groupName.StartsWith("CN"))
                {
                    directorySearcher.Filter = "(&(objectcategory=group)(memberOf=" + groupName + "))";
                }
                else
                {
                    directorySearcher.Filter = "(&(objectcategory=group)(CN=" + groupName + "))";
                }

                try
                {
                    foreach (SearchResult principal in directorySearcher.FindAll())
                    {
                        using (DirectoryEntry directoryEntry = principal.GetDirectoryEntry())
                        {
                            var activeDirectoryGroup = new ActiveDirectoryGroup()
                            {
                                Name = GetValue<string>(directoryEntry, "name"),
                                DistinguishedName = GetValue<string>(directoryEntry, "distinguishedName")
                            };

                            if (activeDirectoryGroup.Name.Length > 0 && activeDirectoryGroup.Name[0] != '.' && activeDirectoryGroup.Name[0] != '*')
                            {
                                activeDirectoryGroup.Name = "*" + activeDirectoryGroup.Name;
                            }

                            if (includeDomain)
                            {
                                SecurityIdentifier sidTokenGroup = new SecurityIdentifier(GetValue<byte[]>(directoryEntry, "objectSid"), 0);
                                NTAccount nt = (NTAccount)sidTokenGroup.Translate(typeof(NTAccount));

                                activeDirectoryGroup.Name = nt.Value.Substring(0, nt.Value.IndexOf('\\') + 1) + activeDirectoryGroup.Name;
                            }

                            if (!groups.Contains(activeDirectoryGroup))
                            {
                                groups.Add(activeDirectoryGroup);

                                if (limit && groups.Count >= MaximumItems)
                                {
                                    break;
                                }

                                if (recursive)
                                {
                                    this.AddGroups(groups, activeDirectoryGroup.DistinguishedName, recursive, limit, includeDomain);
                                }
                            }
                        }
                    }
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                }
            }
        }

        private static void ConfigureDirectorySearcher(DirectorySearcher directorySearcher)
        {
            directorySearcher.ReferralChasing = ReferralChasingOption.All;
            directorySearcher.PropertiesToLoad.Add("givenName");   // first name
            directorySearcher.PropertiesToLoad.Add("sn");          // last name
            directorySearcher.PropertiesToLoad.Add("name");
            directorySearcher.PropertiesToLoad.Add("mail");
            directorySearcher.PropertiesToLoad.Add("objectsid");
        }

        private static T GetValue<T>(DirectoryEntry entry, string name)
        {
            PropertyValueCollection values = entry.Properties[name];

            if (values == null || values.Count == 0)
            {
                return default(T);
            }
            else
            {
                return (T)values.Value;
            }
        }

        private static object[] GetValues(DirectoryEntry entry, string name)
        {
            PropertyValueCollection values = entry.Properties[name];

            if (values == null || values.Count == 0)
            {
                return new object[0];
            }
            else if (values.Count == 1)
            {
                return new object[] { values.Value };
            }
            else
            {
                return (object[])values.Value;
            }
        }

        /// <summary>
        /// Escapes the LDAP search filter to prevent LDAP injection attacks.
        /// </summary>
        /// <param name="searchFilter">The search filter.</param>
        /// <see cref="https://blogs.oracle.com/shankar/entry/what_is_ldap_injection" />
        /// <see cref="http://msdn.microsoft.com/en-us/library/aa746475.aspx" />
        /// <returns>The escaped search filter.</returns>
        private static string EscapeLdapSearchFilter(string searchFilter)
        {
            StringBuilder escape = new StringBuilder();
            for (int i = 0; i < searchFilter.Length; ++i)
            {
                char current = searchFilter[i];
                switch (current)
                {
                    case '\\':
                        escape.Append(@"\5c");
                        break;
                    case '*':
                        escape.Append(@"\2a");
                        break;
                    case '(':
                        escape.Append(@"\28");
                        break;
                    case ')':
                        escape.Append(@"\29");
                        break;
                    case '\u0000':
                        escape.Append(@"\00");
                        break;
                    case '/':
                        escape.Append(@"\2f");
                        break;
                    default:
                        escape.Append(current);
                        break;
                }
            }

            return escape.ToString();
        }

        private ActiveDirectoryUser Convert(DirectoryEntry user, bool includeActiveDirectoryGroups, bool includeDomain)
        {
            ActiveDirectoryUser activeDirectoryUser = new ActiveDirectoryUser()
            {
                FirstName = GetValue<string>(user, "givenName"),
                LastName = GetValue<string>(user, "sn"),
                DisplayName = GetValue<string>(user, "name"),
                Email = GetValue<string>(user, "mail")
            };

            var userGroups = new HashSet<string>();

            if (includeActiveDirectoryGroups)
            {
                user.RefreshCache(new string[] { "tokenGroups" });

                for (int i = 0; i < user.Properties["tokenGroups"].Count; i++)
                {
                    try
                    {
                        SecurityIdentifier sidTokenGroup = new SecurityIdentifier((byte[])user.Properties["tokenGroups"][i], 0);
                        NTAccount nt = (NTAccount)sidTokenGroup.Translate(typeof(NTAccount));

                        string name = nt.Value.Substring(nt.Value.IndexOf('\\') + 1);

                        if (name.Length > 0 && name[0] != '.' && name[0] != '*')
                        {
                            name = "*" + name;
                        }

                        if (includeDomain)
                        {
                            name = nt.Value.Substring(0, nt.Value.IndexOf('\\') + 1) + name;
                        }

                        userGroups.Add(name);
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogError(ex, $"Failed to resolve AD-Group for token (Index: {i})");
                    }
                }
            }

            activeDirectoryUser.Groups = userGroups;

            byte[] objectsid = GetValue<byte[]>(user, "objectsid");
            SecurityIdentifier sid = new SecurityIdentifier(objectsid, 0);

            activeDirectoryUser.LoginName = ((NTAccount)sid.Translate(typeof(NTAccount))).ToString();

            return activeDirectoryUser;
        }

        private DirectorySearcher CreateDirectorySearcher()
        {
            if (!string.IsNullOrEmpty(this.activeDirectorySettings.LdapConnectionString))
            {
                return new DirectorySearcher(new DirectoryEntry(this.activeDirectorySettings.LdapConnectionString));
            }
            else
            {
                Forest currentForest = Forest.GetCurrentForest();
                GlobalCatalog globalCatalog = currentForest.FindGlobalCatalog();
                return globalCatalog.GetDirectorySearcher();
            }
        }
    }
}
