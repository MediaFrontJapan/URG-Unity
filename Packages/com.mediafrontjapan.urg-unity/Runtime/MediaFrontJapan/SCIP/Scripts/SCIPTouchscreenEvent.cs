using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace MediaFrontJapan.SCIP
{
    struct SCIPTouchscreenEvent
    {
        public NativeList<SCIPPointerData> NewPointers;
        public NativeList<SCIPPointerData> AlivedPointers;
        public NativeParallelHashMap<int, SCIPPointerData> DeletedPointers;
        public NativeList<SCIPPointerData> ToState()
        {
            AlivedPointers.AddRange(NewPointers.AsArray());
            NewPointers.Dispose();
            DeletedPointers.Dispose();
            return AlivedPointers;
        }
    }
}
