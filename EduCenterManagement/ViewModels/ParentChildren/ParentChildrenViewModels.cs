namespace EduCenterManagement.ViewModels.ParentChildren;
public sealed class ParentChildrenViewModel{public string LinkingCode{get;init;}="";public int PendingCount{get;init;}public IReadOnlyList<ChildItemViewModel> Children{get;init;}=Array.Empty<ChildItemViewModel>();}
public sealed class ChildItemViewModel{public int Id{get;init;}public string Name{get;init;}="";public int ClassCount{get;init;}}
public sealed class ChildDetailsViewModel{public int Id{get;init;}public string Name{get;init;}="";public IReadOnlyList<ChildClassViewModel> Classes{get;init;}=Array.Empty<ChildClassViewModel>();}
public sealed class ChildClassViewModel{public int Id{get;init;}public string Subject{get;init;}="";public string Name{get;init;}="";public string Teacher{get;init;}="Not assigned";public string Status{get;init;}="";public DateOnly StartDate{get;init;}public DateOnly EndDate{get;init;}public string Schedule{get;init;}="";}
