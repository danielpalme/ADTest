using System.Collections.Generic;

namespace ADTest
{
    public interface IActiveDirectoryService
    {
        ActiveDirectoryUser GetByLogin(string loginName);

        IEnumerable<ActiveDirectoryUser> FindMatchingUsers(string filterText);

        IEnumerable<ActiveDirectoryUser> FindMatchingUsersByGroup(string groupName);

        IEnumerable<ActiveDirectoryGroup> GetGroups(string groupName, bool recursice);

        IEnumerable<ActiveDirectoryGroup> FindMatchingGroups(string filterText);
    }
}
