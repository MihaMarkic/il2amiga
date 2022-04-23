using IL2Amiga.Engine.Attributes;

namespace Cosmos.IL2CPU
{
    public static partial class RuntimeEngine {
		public static uint HeapHandle = 0;
		public const uint InitialHeapSize = 4096;
		public const uint MaximumHeapSize = 10 * 1024 * InitialHeapSize; // 10 megabytes
		public static void InitializeApplication() {
			//Console.WriteLine("Initial Setup");
			// do initialization of all runtime services, like heap and GC..
			InitializeEngine();
			Heap_Initialize();
		}

		public static void InitializeEngine() {
            //int xTest = 5;
			// for replacement by the architecture
		}

		public static void FinalizeEngine() {
			// for replacement by the architecture
		}

		public static void FinalizeApplication(int aExitCode) {
			// finalize all runtime services, like heap and gc
			//Heap_Shutdown();
			FinalizeEngine();
			ExitProcess(aExitCode);			
		}

		public static void ExitProcess(int aExitCode) {
		}
		public static void Heap_Initialize()
		{
			//HeapHandle = PInvokes.Kernel32_HeapCreate(0, InitialHeapSize, MaximumHeapSize);
		}
		[PlugMethod(PlugRequired = true)]
		public static uint Heap_AllocNewObject(uint aSize)
		{
			//			if (aSize == 0) {
			//				aSize = 1;
			//			}
			//return PInvokes.Kernel32_HeapAlloc(HeapHandle, 0x00000008, aSize);
			return 0;
		}
		[PlugMethod(PlugRequired = true)]
		public static void Heap_Free(uint aObject)
		{
			//
		}
		[PlugMethod(PlugRequired = true)]
		public static void Heap_Shutdown()
		{
			//PInvokes.Kernel32_HeapDestroy(HeapHandle);
			//HeapHandle = 0;
		}
	}
}
