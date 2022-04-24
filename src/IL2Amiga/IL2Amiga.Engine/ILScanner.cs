using System.Collections.Immutable;
using System.Reflection;
using IL2Amiga.Engine.Attributes;
using IL2Amiga.Engine.Extensions;
using Microsoft.Extensions.Logging;

namespace IL2Amiga.Engine
{
    public class ILScanner : IDisposable
    {
        readonly ILogger<ILScanner> logger;
        readonly PlugManager plugManager;
        readonly TypeResolver typeResolver;
        //readonly VTablesImplRefs vTablesImplRefs;
        readonly RuntimeEngineRefs runtimeEngineRefs;
        protected ILReader reader;
        readonly AppAssembler assembler;

        // List of assemblies found during scan. We cannot use the list of loaded
        // assemblies because the loaded list includes compilers, etc, and also possibly
        // other unused assemblies. So instead we collect a list of assemblies as we scan.
        internal List<Assembly> usedAssemblies = new List<Assembly>();

        protected HashSet<MemberInfo> items = new HashSet<MemberInfo>(new MemberInfoComparer());
        protected List<object> itemsList = new List<object>();

        // Contains items to be scanned, both types and methods
        protected Queue<ScannerQueueItem> queue = new Queue<ScannerQueueItem>();

        // Virtual methods are nasty and constantly need to be rescanned for
        // overriding methods in new types, so we keep track of them separately.
        // They are also in the main mItems and mQueue.
        //protected HashSet<MethodBase> virtualMethods = new HashSet<MethodBase>();

        protected IDictionary<MethodBase, uint> methodUIDs = new Dictionary<MethodBase, uint>();
        protected IDictionary<Type, uint> typeUIDs = new Dictionary<Type, uint>();

        protected string? mapPathname;
        private bool logEnabled = false;

        protected struct LogItem
        {
            public string SrcType;
            public object Item;
        }

        protected Dictionary<object, List<LogItem>>? logMap;

        //public ILScanner(AppAssembler aAsmblr, TypeResolver typeResolver, Action<Exception> aLogException, Action<string> aLogWarning)
        public ILScanner(ILogger<ILScanner> logger, PlugManager plugManager, TypeResolver typeResolver,// VTablesImplRefs vTablesImplRefs,
            RuntimeEngineRefs runtimeEngineRefs, AppAssembler assembler)
        {
            this.logger = logger;
            this.plugManager = plugManager;
            this.typeResolver = typeResolver;
            //this.vTablesImplRefs = vTablesImplRefs;
            this.runtimeEngineRefs = runtimeEngineRefs;
            reader = new ILReader();
            this.assembler = assembler;
            //vTablesImplRefs.GetTypeId = GetTypeUID; // we need this to figure out which ids object, value type and enum have in the vmt
        }

        public bool EnableLogging(string pathname)
        {
            logMap = new Dictionary<object, List<LogItem>>();
            mapPathname = pathname;

            // be sure that file could be written, to prevent exception on Dispose call, cause we could not make Task log in it
            try
            {
                File.CreateText(pathname).Dispose();
            }
            catch
            {
                return false;
            }
            return true;
        }

        protected void Queue(MemberInfo item, object? src, string srcType, string? sourceItem = null)
        {
            CompilerHelpers.FormatMessage($"Enqueing: {item.DeclaringType?.Name ?? ""}.{item.Name} from {src}");
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            //TODO: fix this, as each label/symbol should also contain an assembly specifier.

            //if ((xMemInfo != null) && (xMemInfo.DeclaringType != null)
            //    && (xMemInfo.DeclaringType.FullName == "System.ThrowHelper")
            //    && (xMemInfo.DeclaringType.Assembly.GetName().Name != "mscorlib"))
            //{
            // System.ThrowHelper exists in MS .NET twice...
            // Its an internal class that exists in both mscorlib and system assemblies.
            // They are separate types though, so normally the scanner scans both and
            // then we get conflicting labels. MS included it twice to make exception
            // throwing code smaller. They are internal though, so we cannot
            // reference them directly and only via finding them as they come along.
            // We find it here, not via QueueType so we only check it here. Later
            // we might have to check in QueueType also.
            // So now we accept both types, but emit code for only one. This works
            // with the current Nasm assembler as we resolve by name in the assembler.
            // However with other assemblers this approach may not work.
            // If AssemblerNASM adds assembly name to the label, this will allow
            // both to exist as they do in BCL.
            // So in the future we might be able to remove this hack, or change
            // how it works.
            //
            // Do nothing
            //
            //}
            /*else*/
            if (!items.Contains(item))
            {
                if (logEnabled)
                {
                    LogMapPoint(src, srcType, item);
                }

                items.Add(item);
                itemsList.Add(item);

                if (src is MethodBase methodBaseSrc)
                {
                    src = $"{methodBaseSrc.DeclaringType}::{src}";
                }

                queue.Enqueue(new ScannerQueueItem(item, srcType, $"{src}\n{sourceItem}"));
            }
        }

        #region Gen2

        public void Execute(MethodBase startMethod, IEnumerable<Assembly> plugsAssemblies)
        {
            // TODO: Investigate using MS CCI
            // Need to check license, as well as in profiler
            // http://cciast.codeplex.com/

            #region Description

            // Methodology
            //
            // Ok - we've done the scanner enough times to know it needs to be
            // documented super well so that future changes won't inadvertently
            // break undocumented and unseen requirements.
            //
            // We've tried many approaches including recursive and additive scanning.
            // They typically end up being inefficient, overly complex, or both.
            //
            // -We would like to scan all types/methods so we can plug them.
            // -But we can't scan them until we plug them, because we will scan things
            // that plugs would remove/change the paths of.
            // -Plugs may also call methods which are also plugged.
            // -We cannot resolve plugs ahead of time but must do on the fly during
            // scanning.
            // -TODO: Because we do on the fly resolution, we need to add explicit
            // checking of plug classes and err when public methods are found that
            // do not resolve. Maybe we can make a list and mark, or rescan. Can be done
            // later or as an optional auditing step.
            //
            // This why in the past we had repetitive scans.
            //
            // Now we focus on more passes, but simpler execution. In the end it should
            // be easier to optimize and yield overall better performance. Most of the
            // passes should be low overhead versus an integrated system which often
            // would need to reiterate over items multiple times. So we do more loops on
            // with less repetitive analysis, instead of fewer loops but more repetition.
            //
            // -Locate all plug classes
            // -Scan from entry point collecting all types and methods while checking
            // for and following plugs
            // -For each type
            //    -Include all ancestors
            //    -Include all static constructors
            // -For each virtual method
            //    -Scan overloads in descendants until IsFinal, IsSealed or end
            //    -Scan base in ancestors until top or IsAbstract
            // -Go to scan types again, until no new ones found.
            // -Because the virtual method scanning will add to the list as it goes, maintain
            //  2 lists.
            //    -Known Types and Methods
            //    -Types and Methods in Queue - to be scanned
            // -Finally, do compilation

            #endregion Description

            plugManager.FindPlugImplementors(plugsAssemblies);
            // Now that we found all plugs, scan them.
            // We have to scan them after we find all plugs, because
            // plugs can use other plugs
            plugManager.ScanFoundPlugs();
            foreach (var plug in plugManager.PlugImplementors)
            {
                logger.Log(LogLevel.Debug, CompilerHelpers.FormatMessage($"Plug found: '{plug.Key.FullName}' in '{plug.Key.Assembly.FullName}'"));
            }

            //ILOp.PlugManager = mPlugManager;

            //// Pull in extra implementations, GC etc.
            //Queue(RuntimeEngineRefs.InitializeApplicationRef, null, "Explicit Entry");
            //Queue(RuntimeEngineRefs.FinalizeApplicationRef, null, "Explicit Entry");
            //Queue(VTablesImplRefs.IsInstanceRef, null, "Explicit Entry");
            //Queue(VTablesImplRefs.SetTypeInfoRef, null, "Explicit Entry");
            //Queue(VTablesImplRefs.SetInterfaceInfoRef, null, "Explicit Entry");
            //Queue(VTablesImplRefs.SetMethodInfoRef, null, "Explicit Entry");
            //Queue(VTablesImplRefs.SetInterfaceMethodInfoRef, null, "Explicit Entry");
            //Queue(VTablesImplRefs.GetMethodAddressForTypeRef, null, "Explicit Entry");
            //Queue(VTablesImplRefs.GetMethodAddressForInterfaceTypeRef, null, "Explicit Entry");
            //Queue(VTablesImplRefs.GetDeclaringTypeOfMethodForTypeRef, null, "Explicit Entry");
            //Queue(GCImplementationRefs.InitRef, null, "Explicit Entry");
            //Queue(GCImplementationRefs.IncRootCountRef, null, "Explicit Entry");
            //Queue(GCImplementationRefs.IncRootCountsInStructRef, null, "Explicit Entry");
            //Queue(GCImplementationRefs.DecRootCountRef, null, "Explicit Entry");
            //Queue(GCImplementationRefs.DecRootCountsInStructRef, null, "Explicit Entry");
            //Queue(GCImplementationRefs.AllocNewObjectRef, null, "Explicit Entry");
            //// for now, to ease runtime exception throwing
            //Queue(typeof(ExceptionHelper).GetMethod("ThrowNotImplemented", new Type[] { typeof(string) }, null), null, "Explicit Entry");
            //Queue(typeof(ExceptionHelper).GetMethod("ThrowOverflow", Type.EmptyTypes, null), null, "Explicit Entry");
            //Queue(typeof(ExceptionHelper).GetMethod("ThrowInvalidOperation", new Type[] { typeof(string) }, null), null, "Explicit Entry");
            //Queue(typeof(ExceptionHelper).GetMethod("ThrowArgumentOutOfRange", new Type[] { typeof(string) }, null), null, "Explicit Entry");

            //// register system types:
            //Queue(typeof(Array), null, "Explicit Entry");
            //Queue(typeof(Array).Assembly.GetType("System.SZArrayHelper"), null, "Explicit Entry");
            //Queue(typeof(Array).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).First(), null, "Explicit Entry");
            //Queue(typeof(MulticastDelegate).GetMethod("GetInvocationList"), null, "Explicit Entry");
            //Queue(ExceptionHelperRefs.CurrentExceptionRef, null, "Explicit Entry");
            //Queue(ExceptionHelperRefs.ThrowInvalidCastExceptionRef, null, "Explicit Entry");
            //Queue(ExceptionHelperRefs.ThrowNotFiniteNumberExceptionRef, null, "Explicit Entry");
            //Queue(ExceptionHelperRefs.ThrowDivideByZeroExceptionRef, null, "Explicit Entry");
            //Queue(ExceptionHelperRefs.ThrowIndexOutOfRangeException, null, "Explicit Entry");

            //mAsmblr.ProcessField(typeof(string).GetField("Empty", BindingFlags.Static | BindingFlags.Public));

            // Start from entry point of this program
            Queue(startMethod, null, "Entry Point");

            ScanQueue();
            UpdateAssemblies();
            Assemble();

            assembler.EmitEntrypoint(startMethod);
        }

        #endregion Gen2

        #region Gen3

        public void Execute(MethodBase[] aBootEntries, List<MemberInfo> aForceIncludes, IEnumerable<Assembly> plugsAssemblies)
        {
            foreach (var xBootEntry in aBootEntries)
            {
                Queue(xBootEntry.DeclaringType, null, "Boot Entry Declaring Type");
                Queue(xBootEntry, null, "Boot Entry");
            }

            foreach (var xForceInclude in aForceIncludes)
            {
                Queue(xForceInclude, null, "Force Include");
            }

            plugManager.FindPlugImplementors(plugsAssemblies);
            // Now that we found all plugs, scan them.
            // We have to scan them after we find all plugs, because
            // plugs can use other plugs
            plugManager.ScanFoundPlugs();
            foreach (var xPlug in plugManager.PlugImplementors)
            {
                logger.Log(LogLevel.Debug, "Plug found: '{plug}' in '{assembly}'", xPlug.Key.FullName, xPlug.Key.Assembly.FullName);
            }

            // Pull in extra implementations, GC etc.
            Queue(runtimeEngineRefs.InitializeApplicationRef, null, "Explicit Entry");
            Queue(runtimeEngineRefs.FinalizeApplicationRef, null, "Explicit Entry");
            //Queue(vTablesImplRefs.SetMethodInfoRef, null, "Explicit Entry");
            //Queue(vTablesImplRefs.IsInstanceRef, null, "Explicit Entry");
            //Queue(vTablesImplRefs.SetTypeInfoRef, null, "Explicit Entry");
            //Queue(vTablesImplRefs.SetInterfaceInfoRef, null, "Explicit Entry");
            //Queue(vTablesImplRefs.SetInterfaceMethodInfoRef, null, "Explicit Entry");
            //Queue(vTablesImplRefs.GetMethodAddressForTypeRef, null, "Explicit Entry");
            //Queue(vTablesImplRefs.GetMethodAddressForInterfaceTypeRef, null, "Explicit Entry");
            //Queue(GCImplementationRefs.AllocNewObjectRef, null, "Explicit Entry");
            // Pull in Array constructor
            Queue(typeof(Array).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).First(), null, "Explicit Entry");
            // Pull in MulticastDelegate.GetInvocationList, needed by the Invoke plug
            Queue(typeof(MulticastDelegate).GetMethod("GetInvocationList"), null, "Explicit Entry");

            assembler.ProcessField(typeof(string).GetField("Empty", BindingFlags.Static | BindingFlags.Public) ?? throw new Exception("Couldn't find Empty"));

            ScanQueue();
            UpdateAssemblies();
            Assemble();

            assembler.EmitEntrypoint(null, aBootEntries.ToImmutableArray());
        }

        #endregion Gen3

        public void QueueMethod(MethodBase method)
        {
            Queue(method, null, "Explicit entry via QueueMethod");
        }

        /// This method changes the opcodes. Changes are:
        /// * inserting the ValueUID for method ops.
        public void ProcessInstructions(ImmutableArray<ILOpCode> opCodes) // to remove -------
        {
            foreach (var opCode in opCodes)
            {
                if (opCode is ILOpCodes.OpMethod opMethod)
                {
                    items.TryGetValue(opMethod.Value, out MemberInfo? value);
                    opMethod.Value = (MethodBase)(value ?? opMethod.Value);
                    opMethod.ValueUID = GetMethodUID(opMethod.Value);
                }
            }
        }

        public void Dispose()
        {
            //if (logEnabled)
            //{
            //    // Create bookmarks, but also a dictionary that
            //    // we can find the items in
            //    var bookmarks = new Dictionary<object, int>();
            //    int bookmark = 0;
            //    if (logMap is not null)
            //    {
            //        foreach (var xList in logMap)
            //        {
            //            foreach (var xItem in xList.Value)
            //            {
            //                bookmarks.Add(xItem.Item, bookmark);
            //                bookmark++;
            //            }
            //        }

            //        if (!string.IsNullOrWhiteSpace(mapPathname))
            //        {
            //            using (logWriter = new StreamWriter(File.OpenWrite(mapPathname)))
            //            {
            //                logWriter.WriteLine("<html><body>");
            //                foreach (var list in logMap)
            //                {
            //                    var xLogItemText = LogItemText(list.Key);

            //                    logWriter.WriteLine("<hr>");

            //                    // Emit bookmarks above source, so when clicking links user doesn't need
            //                    // to constantly scroll up.
            //                    foreach (var xItem in list.Value)
            //                    {
            //                        logWriter.WriteLine("<a name=\"Item" + bookmarks[xItem.Item].ToString() + "_S\"></a>");
            //                    }

            //                    if (!bookmarks.TryGetValue(list.Key, out var xHref))
            //                    {
            //                        xHref = -1;
            //                    }
            //                    logWriter.Write("<p>");
            //                    if (xHref >= 0)
            //                    {
            //                        logWriter.WriteLine("<a href=\"#Item" + xHref.ToString() + "_S\">");
            //                        logWriter.WriteLine("<a name=\"Item{0}\">", xHref);
            //                    }
            //                    if (list.Key == null)
            //                    {
            //                        logWriter.WriteLine("Unspecified Source");
            //                    }
            //                    else
            //                    {
            //                        logWriter.WriteLine(xLogItemText);
            //                    }
            //                    if (xHref >= 0)
            //                    {
            //                        logWriter.Write("</a>");
            //                        logWriter.Write("</a>");
            //                    }
            //                    logWriter.WriteLine("</p>");

            //                    logWriter.WriteLine("<ul>");
            //                    foreach (var xItem in list.Value)
            //                    {
            //                        logWriter.Write("<li><a href=\"#Item{1}\">{0}</a></li>", LogItemText(xItem.Item), bookmarks[xItem.Item]);

            //                        logWriter.WriteLine("<ul>");
            //                        logWriter.WriteLine("<li>" + xItem.SrcType + "</li>");
            //                        logWriter.WriteLine("</ul>");
            //                    }
            //                    logWriter.WriteLine("</ul>");
            //                }
            //                logWriter.WriteLine("</body></html>");
            //            }
            //        }
            //    }
            //}
        }

        public int MethodCount => methodUIDs.Count;

        protected string LogItemText(object item)
        {
            if (item is MethodBase mb)
            {
                return $"Method: {mb.DeclaringType}.{mb.Name}<br>{mb.GetFullName()}";
            }
            if (item is Type)
            {
                var x = (Type)item;
                return $"Type: {x.FullName}";
            }
            return $"Other: {item}";
        }

        protected void ScanMethod(MethodBase method, bool isPlug, string sourceItem)
        {
            logger.Log(LogLevel.Debug, CompilerHelpers.FormatMessage($"ILScanner: ScanMethod"));
            logger.Log(LogLevel.Debug, CompilerHelpers.FormatMessage($"Method = '{method}'"));
            logger.Log(LogLevel.Debug, CompilerHelpers.FormatMessage($"IsPlug = '{isPlug}'"));
            logger.Log(LogLevel.Debug, CompilerHelpers.FormatMessage($"Source = '{sourceItem}'"));

            var parameters = method.GetParameters();
            var paramTypes = new Type[parameters.Length];
            // Dont use foreach, enum generally keeps order but
            // isn't guaranteed.
            //string xMethodFullName = LabelName.GetFullName(aMethod);

            for (int i = 0; i < parameters.Length; i++)
            {
                paramTypes[i] = parameters[i].ParameterType;
                Queue(paramTypes[i], method, "Parameter");
            }
            var isDynamicMethod = method.DeclaringType == null;
            // Queue Types directly related to method
            if (!isPlug)
            {
                // Don't queue declaring types of plugs
                if (!isDynamicMethod)
                {
                    // dont queue declaring types of dynamic methods either, those dont have a declaring type
                    Queue(method.DeclaringType!, method, "Declaring Type");
                }
            }
            if (method is MethodInfo methodInfo)
            {
                Queue(methodInfo.ReturnType, method, "Return Type");
            }
            // Scan virtuals

            #region Virtuals scan

            //if (!isDynamicMethod && method.IsVirtual)
            //{
            //    // For virtuals we need to climb up the type tree
            //    // and find the top base method. We then add that top
            //    // node to the mVirtuals list. We don't need to add the
            //    // types becuase adding DeclaringType will already cause
            //    // all ancestor types to be added.

            //    var virtMethod = method;
            //    var virtType = method.DeclaringType;
            //    MethodBase newVirtMethod;
            //    while (true)
            //    {
            //        virtType = virtType.BaseType;
            //        if (virtType == null)
            //        {
            //            // We've reached object, can't go farther
            //            newVirtMethod = null;
            //        }
            //        else
            //        {
            //            newVirtMethod = virtType.GetMethod(method.Name, xParamTypes);
            //            if (newVirtMethod != null)
            //            {
            //                if (!newVirtMethod.IsVirtual)
            //                {
            //                    // This can happen if a virtual "replaces" a non virtual
            //                    // above it that is not virtual.
            //                    newVirtMethod = null;
            //                }
            //            }
            //        }
            //        // We dont bother to add these to Queue, because we have to do a
            //        // full downlevel scan if its a new base virtual anyways.
            //        if (newVirtMethod == null)
            //        {
            //            // If its already in the list, we mark it null
            //            // so we dont do a full downlevel scan.
            //            if (virtualMethods.Contains(virtMethod))
            //            {
            //                virtMethod = null;
            //            }
            //            break;
            //        }
            //        virtMethod = newVirtMethod;
            //    }

            //    // New virtual base found, we need to downscan it
            //    // If it was already in mVirtuals, then ScanType will take
            //    // care of new additions.
            //    if (virtMethod != null)
            //    {
            //        Queue(virtMethod, method, "Virtual Base");
            //        virtualMethods.Add(virtMethod);

            //        // List changes as we go, cant be foreach
            //        for (int i = 0; i < mItemsList.Count; i++)
            //        {
            //            if (mItemsList[i] is Type xType && xType != virtMethod.DeclaringType && !xType.IsInterface)
            //            {
            //                if (xType.IsSubclassOf(virtMethod.DeclaringType))
            //                {
            //                    var enumerable = xType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            //                                          .Where(method => method.Name == method.Name
            //                                                           && method.GetParameters().Select(param => param.ParameterType).SequenceEqual(xParamTypes));
            //                    // We need to check IsVirtual, a non virtual could
            //                    // "replace" a virtual above it?
            //                    var xNewMethod = enumerable.FirstOrDefault(m => m.IsVirtual);
            //                    while (xNewMethod != null && (xNewMethod.Attributes & MethodAttributes.NewSlot) != 0)
            //                    {
            //                        xType = xType.BaseType;
            //                        xNewMethod = enumerable.Where(m => m.DeclaringType == xType).SingleOrDefault();
            //                    }
            //                    if (xNewMethod != null)
            //                    {
            //                        Queue(xNewMethod, method, "Virtual Downscan");
            //                    }
            //                }
            //                else if (virtMethod.DeclaringType.IsInterface
            //                      && xType.GetInterfaces().Contains(virtMethod.DeclaringType)
            //                      && (xType.BaseType != typeof(Array) || !virtMethod.DeclaringType.IsGenericType))
            //                {
            //                    var xInterfaceMap = xType.GetInterfaceMap(virtMethod.DeclaringType);
            //                    var xMethodIndex = Array.IndexOf(xInterfaceMap.InterfaceMethods, virtMethod);

            //                    if (xMethodIndex != -1)
            //                    {
            //                        var xMethod = xInterfaceMap.TargetMethods[xMethodIndex];

            //                        if (xMethod.DeclaringType == xType)
            //                        {
            //                            Queue(xInterfaceMap.TargetMethods[xMethodIndex], method, "Virtual Downscan");
            //                        }
            //                    }
            //                }
            //            }
            //        }
            //    }
            //}

            #endregion Virtuals scan

            MethodBase? plug = null;
            // Plugs may use plugs, but plugs won't be plugged over them self
            var inl = method.GetCustomAttribute<InlineAttribute>();
            if (!isPlug && !isDynamicMethod)
            {
                // Check to see if method is plugged, if it is we don't scan body
                plug = plugManager.ResolvePlug(method, paramTypes);
                if (plug != null)
                {
                    ScanMethod(plug, true, "Plug method");
                    if (inl == null)
                    {
                        Queue(plug, method, "Plug method");
                    }
                }
            }

            if (plug is null)
            {
                bool needsPlug = false;
                if ((method.Attributes & MethodAttributes.PinvokeImpl) != 0)
                {
                    // pInvoke methods dont have an embedded implementation
                    needsPlug = true;
                }
                else
                {
                    var implFlags = method.GetMethodImplementationFlags();
                    // todo: prob even more
                    if (implFlags.HasFlag(MethodImplAttributes.Native) || implFlags.HasFlag(MethodImplAttributes.InternalCall))
                    {
                        // native implementations cannot be compiled
                        needsPlug = true;
                    }
                }
                if (needsPlug)
                {
                    throw new Exception(Environment.NewLine
                        + "Native code encountered, plug required." + Environment.NewLine
                                        + "  DO NOT REPORT THIS AS A BUG." + Environment.NewLine
                                        + "  Please see http://www.gocosmos.org/docs/plugs/missing/" + Environment.NewLine
                        // + "  Need plug for: " + LabelName.GetFullName(method) + "(Plug Signature: " + DataMember.FilterStringForIncorrectChars(LabelName.GetFullName(method)) + " ). " + Environment.NewLine
                        // + "  Static: " + method.IsStatic + Environment.NewLine
                        // + "  Assembly: " + method.DeclaringType.Assembly.FullName + Environment.NewLine
                        // + "  Called from:" + Environment.NewLine + sourceItem + Environment.NewLine
                        );
                }

                //TODO: As we scan each method, we could update or put in a new list
                // that has the resolved plug so we don't have to re-resolve it again
                // later for compilation.

                // Scan the method body for more type and method refs
                //TODO: Dont queue new items if they are plugged
                // or do we need to queue them with a resolved ref in a new list?

                if (inl is not null)
                {
                    return; // cancel inline
                }

                var opCodes = reader.ProcessMethod(method);
                ProcessInstructions(opCodes);
                foreach (var opCode in opCodes)
                {
                    if (opCode is ILOpCodes.OpMethod opMethod)
                    {
                        Queue(opMethod.Value, method, "Call", sourceItem);
                    }
                    else if (opCode is ILOpCodes.OpType opType)
                    {
                        Queue(opType.Value, method, "OpCode Value");
                    }
                    else if (opCode is ILOpCodes.OpField opField)
                    {
                        //TODO: Need to do this? Will we get a ILOpCodes.OpType as well?
                        Queue(opField.Value.DeclaringType!, method, "OpCode Value");
                        if (opField.Value.IsStatic)
                        {
                            //TODO: Why do we add static fields, but not instance?
                            // AW: instance fields are "added" always, as part of a type, but for static fields, we need to emit a data member
                            Queue(opField.Value, method, "OpCode Value");
                        }
                    }
                    else if (opCode is ILOpCodes.OpToken opToken)
                    {
                        if (opToken.ValueIsType)
                        {
                            Queue(opToken.ValueType!, method, "OpCode Value");
                        }
                        if (opToken.ValueIsField)
                        {
                            Queue(opToken.ValueField!.DeclaringType!, method, "OpCode Value");
                            if (opToken.ValueField.IsStatic)
                            {
                                //TODO: Why do we add static fields, but not instance?
                                // AW: instance fields are "added" always, as part of a type, but for static fields, we need to emit a data member
                                Queue(opToken.ValueField, method, "OpCode Value");
                            }
                        }
                    }
                }
            }
        }

        protected void ScanType(Type type)
        {
            CompilerHelpers.FormatMessage($"ILScanner: ScanType");
            CompilerHelpers.FormatMessage($"Type = '{type}'");

            // Add immediate ancestor type
            // We dont need to crawl up farther, when the BaseType is scanned
            // it will add its BaseType, and so on.
            if (type.BaseType is not null)
            {
                Queue(type.BaseType, type, "Base Type");
            }
            // Queue static ctors
            // We always need static ctors, else the type cannot
            // be created.
            foreach (var ctor in type.GetConstructors(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
            {
                if (ctor.DeclaringType == type)
                {
                    Queue(ctor, type, "Static Constructor");
                }
            }

            if (type.BaseType == typeof(Array) && !type.GetElementType()!.IsPointer)
            {
                var szArrayHelper = typeof(Array).Assembly.GetType("System.SZArrayHelper"); // We manually add the link to the generic interfaces for an array
                foreach (var method in szArrayHelper!.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    Queue(method.MakeGenericMethod(new Type[] { type.GetElementType()! }), type, "Virtual SzArrayHelper");
                }
            }


            // Scam Fields so that we include those types
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                Queue(field.FieldType, type, "Field Type");
            }

            // For each new type, we need to scan for possible new virtuals
            // in our new type if its a descendant of something in
            // mVirtuals.
            //foreach (var virt in virtualMethods)
            //{
            //    // See if our new type is a subclass of any virt's DeclaringTypes
            //    // If so our new type might have some virtuals
            //    if (type.IsSubclassOf(virt.DeclaringType!))
            //    {
            //        var xParams = virt.GetParameters();
            //        var xParamTypes = new Type[xParams.Length];
            //        // Dont use foreach, enum generaly keeps order but
            //        // isn't guaranteed.
            //        for (int i = 0; i < xParams.Length; i++)
            //        {
            //            xParamTypes[i] = xParams[i].ParameterType;
            //        }
            //        var xMethod = type.GetMethod(virt.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, xParamTypes, null);
            //        if (xMethod != null)
            //        {
            //            // We need to check IsVirtual, a non virtual could
            //            // "replace" a virtual above it?
            //            if (xMethod.IsVirtual)
            //            {
            //                Queue(xMethod, type, "Virtual");
            //            }
            //        }
            //    }
            //    else if (!type.IsGenericParameter && virt.DeclaringType.IsInterface && !(type.BaseType == typeof(Array) && virt.DeclaringType.IsGenericType))
            //    {
            //        if (!type.IsInterface && type.GetInterfaces().Contains(virt.DeclaringType))
            //        {
            //            var xIntfMapping = type.GetInterfaceMap(virt.DeclaringType);
            //            if (xIntfMapping.InterfaceMethods != null && xIntfMapping.TargetMethods != null)
            //            {
            //                var xIdx = Array.IndexOf(xIntfMapping.InterfaceMethods, virt);
            //                if (xIdx != -1)
            //                {
            //                    Queue(xIntfMapping.TargetMethods[xIdx], type, "Virtual");
            //                }
            //            }
            //        }
            //    }
            //}

            foreach (var @interface in type.GetInterfaces())
            {
                Queue(@interface, type, "Implemented Interface");
            }
        }

        protected void ScanQueue()
        {
            while (queue.Count > 0)
            {
                var item = queue.Dequeue();
                CompilerHelpers.FormatMessage($"ILScanner: ScanQueue - '{item}'");
                // Check for MethodBase first, they are more numerous
                // and will reduce compares
                if (item.Item is MethodBase method)
                {
                    ScanMethod(method, false, item.SourceItem);
                }
                else if (item.Item is Type type)
                {
                    ScanType(type);

                    // Methods and fields cant exist without types, so we only update
                    // mUsedAssemblies in type branch.
                    if (!usedAssemblies.Contains(type.Assembly))
                    {
                        usedAssemblies.Add(type.Assembly);
                    }
                }
                else if (item.Item is FieldInfo)
                {
                    // todo: static fields need more processing?
                }
                else
                {
                    throw new Exception("Unknown item found in queue.");
                }
            }
        }

        protected void LogMapPoint(object? src, string srcType, object item)
        {
            // Keys cant be null. If null, we just say ILScanner is the source
            if (src == null)
            {
                src = typeof(ILScanner);
            }

            var logItem = new LogItem
            {
                SrcType = srcType,
                Item = item
            };
            if (logMap?.TryGetValue(src, out var list) != true)
            {
                list = new List<LogItem>();
                logMap!.Add(src, list);
            }
            list!.Add(logItem);
        }

        private MethodInfo GetUltimateBaseMethod(MethodInfo method)
        {
            var baseMethod = method;
            while (true)
            {
                var baseDefinition = baseMethod.GetBaseDefinition();
                if (baseDefinition == baseMethod)
                {
                    return baseMethod;
                }
                baseMethod = baseDefinition;
            }
        }

        readonly static MethodBaseComparer methodBaseComparer = new MethodBaseComparer();
        protected uint GetMethodUID(MethodBase method)
        {
            if (methodUIDs.TryGetValue(method, out var methodUID))
            {
                return methodUID;
            }
            else
            {
                if (!method.DeclaringType!.IsInterface)
                {
                    if (method is MethodInfo methodInfo)
                    {
                        var baseMethod = GetUltimateBaseMethod(methodInfo);

                        if (!methodUIDs.TryGetValue(baseMethod, out methodUID))
                        {
                            methodUID = (uint)methodUIDs.Count;
                            methodUIDs.Add(baseMethod, methodUID);
                        }

                        if (!methodBaseComparer.Equals(method, baseMethod))
                        {
                            methodUIDs.Add(method, methodUID);
                        }

                        return methodUID;
                    }
                }

                methodUID = (uint)methodUIDs.Count;
                methodUIDs.Add(method, methodUID);

                return methodUID;
            }
        }

        protected uint GetTypeUID(Type type)
        {
            if (!items.Contains(type))
            {
                throw new Exception($"Cannot get UID of types which are not queued! Type: {type.Name}");
            }
            if (!typeUIDs.ContainsKey(type))
            {
                var xId = (uint)typeUIDs.Count;
                typeUIDs.Add(type, xId);
                return xId;
            }
            return typeUIDs[type];
        }

        protected void UpdateAssemblies()
        {
            // It would be nice to keep DebugInfo output into assembler only but
            // there is so much info that is available in scanner that is needed
            // or can be used in a more efficient manner. So we output in both
            // scanner and assembler as needed.

            //mAsmblr.DebugInfo.AddAssemblies(usedAssemblies);
        }

        protected void Assemble()
        {
            foreach (var item in items)
            {
                if (item is MethodBase method)
                {
                    var @params = method.GetParameters();
                    var paramTypes = @params.Select(q => q.ParameterType).ToArray();
                    MethodBase? plug = plugManager.ResolvePlug(method, paramTypes);
                    var methodType = Il2cpuMethodInfo.TypeEnum.Normal;
                    Type? plugAssembler = null;
                    Il2cpuMethodInfo? plugInfo = null;
                    var methodInline = method.GetCustomAttribute<InlineAttribute>();
                    if (methodInline is not null)
                    {
                        // inline assembler, shouldn't come here..
                        continue;
                    }
                    var methodIdMethod = itemsList.IndexOf(method);
                    if (methodIdMethod == -1)
                    {
                        throw new Exception("Method not in scanner list!");
                    }
                    PlugMethod? plugAttrib = null;
                    if (plug != null)
                    {
                        methodType = Il2cpuMethodInfo.TypeEnum.NeedsPlug;
                        plugAttrib = plug.GetCustomAttribute<PlugMethod>();
                        var inlineAttrib = plug.GetCustomAttribute<InlineAttribute>();
                        var xMethodIdPlug = itemsList.IndexOf(plug);
                        if (xMethodIdPlug == -1 && inlineAttrib is null)
                        {
                            throw new Exception("Plug method not in scanner list!");
                        }
                        if (plugAttrib is not null && inlineAttrib is null)
                        {
                            plugAssembler = plugAttrib.Assembler;
                            plugInfo = new Il2cpuMethodInfo(plug, (uint)xMethodIdPlug, Il2cpuMethodInfo.TypeEnum.Plug, null, plugAssembler);

                            var xMethodInfo = new Il2cpuMethodInfo(method, (uint)methodIdMethod, methodType, plugInfo);
                            if (plugAttrib.IsWildcard)
                            {
                                plugInfo.IsWildcard = true;
                                plugInfo.PluggedMethod = xMethodInfo;
                                var xInstructions = reader.ProcessMethod(plug);
                                if (xInstructions.Length > 0)
                                {
                                    ProcessInstructions(xInstructions);
                                    assembler.ProcessMethod(plugInfo, xInstructions);
                                }
                            }
                            assembler.GenerateMethodForward(xMethodInfo, plugInfo);
                        }
                        else
                        {
                            if (inlineAttrib is not null)
                            {
                                var xMethodID = itemsList.IndexOf(item);
                                if (xMethodID == -1)
                                {
                                    throw new Exception("Method not in list!");
                                }
                                plugInfo = new Il2cpuMethodInfo(plug, (uint)xMethodID, Il2cpuMethodInfo.TypeEnum.Plug, null, true);

                                var xMethodInfo = new Il2cpuMethodInfo(method, (uint)methodIdMethod, methodType, plugInfo);

                                plugInfo.PluggedMethod = xMethodInfo;
                                var xInstructions = reader.ProcessMethod(plug);
                                if (xInstructions != null)
                                {
                                    ProcessInstructions(xInstructions);
                                    assembler.ProcessMethod(plugInfo, xInstructions);
                                }
                                assembler.GenerateMethodForward(xMethodInfo, plugInfo);
                            }
                            else
                            {
                                plugInfo = new Il2cpuMethodInfo(plug, (uint)xMethodIdPlug, Il2cpuMethodInfo.TypeEnum.Plug, null, plugAssembler);

                                var xMethodInfo = new Il2cpuMethodInfo(method, (uint)methodIdMethod, methodType, plugInfo);
                                assembler.GenerateMethodForward(xMethodInfo, plugInfo);
                            }
                        }
                    }
                    else
                    {
                        plugAttrib = method.GetCustomAttribute<PlugMethod>();

                        if (plugAttrib != null)
                        {
                            if (plugAttrib.IsWildcard)
                            {
                                continue;
                            }
                            if (plugAttrib.PlugRequired)
                            {
                                throw new Exception(string.Format("Method {0} requires a plug, but none is implemented", method.Name));
                            }
                            plugAssembler = plugAttrib.Assembler;
                        }

                        var xMethodInfo = new Il2cpuMethodInfo(method, (uint)methodIdMethod, methodType, plugInfo, plugAssembler);
                        var xInstructions = reader.ProcessMethod(method);
                        if (xInstructions != null)
                        {
                            ProcessInstructions(xInstructions);
                            assembler.ProcessMethod(xMethodInfo, xInstructions);
                        }
                    }
                }
                else if (item is FieldInfo)
                {
                    assembler.ProcessField((FieldInfo)item);
                }
            }

            var xTypes = new HashSet<Type>();
            var xMethods = new HashSet<MethodBase>(new MethodBaseComparer());
            foreach (var xItem in items)
            {
                if (xItem is MethodBase)
                {
                    xMethods.Add((MethodBase)xItem);
                }
                else if (xItem is Type)
                {
                    xTypes.Add((Type)xItem);
                }
            }

            assembler.GenerateVMTCode(xTypes, xMethods, GetTypeUID, GetMethodUID);
        }
    }
}
