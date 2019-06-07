using System.Collections.Generic;

namespace ADTest
{
    public interface IActiveDirectoryUserService
    {
        ActiveDirectoryUser FindByLogin(string loginName);

        IEnumerable<ActiveDirectoryUser> FindMatchingUsers(string filterText);

        IEnumerable<ActiveDirectoryUser> FindMatchingUsersByGroup(string groupName);

        IEnumerable<string> GetGroupWithChildGroups(string groupName);
    }
}
