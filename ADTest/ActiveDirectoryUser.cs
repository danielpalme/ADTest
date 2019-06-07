using System.Collections.Generic;

namespace ADTest
{
    public class ActiveDirectoryUser
    {
        private string loginName;

        public string LoginName
        {
            get
            {
                return this.loginName;
            }

            set
            {
                this.loginName = value == null ? null : value.ToLowerInvariant();
            }
        }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string DisplayName { get; set; }

        public string Email { get; set; }

        public IEnumerable<string> Groups { get; internal set; } = new string[0];

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            return string.Equals(this.LoginName, (obj as ActiveDirectoryUser).LoginName);
        }

        public override int GetHashCode()
        {
            return this.LoginName.GetHashCode();
        }
    }
}
