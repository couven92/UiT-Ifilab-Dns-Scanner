using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
#region AssemblyConfiguration("$(Configuration)")]
#if DEBUG
[assembly: AssemblyConfiguration("Debug Configuration")]
// #endif // DEBUG
#else // !DEBUG
[assembly: AssemblyConfiguration("Release Configuration")]
#endif // !DEBUG
#endregion // AssemblyConfiguration("$(Configuration)")]
[assembly: AssemblyCompany("UiT The arctic university of Norway\r\nDepartment of Computer Science\r\nFredrik Høisæther Rasch")]
[assembly: AssemblyProduct("Ifilab DNS scanner")]
[assembly: AssemblyTrademark("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("0e494a13-9d40-4964-bef5-f7a77360f957")]
