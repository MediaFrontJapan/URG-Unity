namespace MediaFrontJapan.SCIP
{
    sealed class SCIPScanPlaneClampDistancesDrawer : SCIPScanPlaneDistancesDrawer
    {
        protected override void OnEnable()
        {
            base.OnEnable();
            scanPlane.ClampDistancesChanged += SetDistances;
            if (scanPlane.ClampDistances.IsCreated)
            {
                SetDistances(scanPlane.ClampDistances);
            }
        }

        protected override void OnDisable()
        {
            if (scanPlane)
            {
                scanPlane.ClampDistancesChanged -= SetDistances;
            }

            base.OnDisable();
        }
    }
}
