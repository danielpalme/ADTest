namespace ADTest
{
    public class ActiveDirectoryGroup
    {
        public string Name { get; set; }

        public string DistinguishedName { get; set; }

        public override string ToString()
        {
            return this.DistinguishedName;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || this.GetType() != obj.GetType())
            {
                return false;
            }

            return string.Equals(this.DistinguishedName, (obj as ActiveDirectoryGroup).DistinguishedName);
        }

        public override int GetHashCode()
        {
            return this.DistinguishedName.GetHashCode();
        }
    }
}
