# ADTest

Helper tool to query *Active Directory*

```
 -users SEARCH
 -userdetails LOGINNAME
 -usersingroup GROUP
 -groups GROUP
 -groupsrecursive GROUP
```

## List AD-Groups in Controller / View
```csharp
var groups = (this.User.Identity as System.Security.Principal.WindowsIdentity).Groups
    .Select(g => g.Translate(typeof(System.Security.Principal.NTAccount)))
    .ToList();
```

Author: Daniel Palme  
Blog: [www.palmmedia.de](http://www.palmmedia.de)  
Twitter: [@danielpalme](http://twitter.com/danielpalme)  