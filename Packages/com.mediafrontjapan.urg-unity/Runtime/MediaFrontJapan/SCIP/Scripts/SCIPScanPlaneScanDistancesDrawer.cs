namespace MediaFrontJapan.SCIP
{
    sealed class SCIPScanPlaneScanDistancesDrawer : SCIPScanPlaneDistancesDrawer
    {
        protected override void OnEnable()
        {
            base.OnEnable();
            scanPlane.Client.CaptureChanged += SetCapture;
            SetCapture(scanPlane.Client.Capture);
        }

        protected override void OnDisable()
        {
            if (scanPlane && scanPlane.Client)
            {
                scanPlane.Client.CaptureChanged -= SetCapture;
            }

            base.OnDisable();
        }

        void SetCapture(SCIPCapture capture)
        {
            SetDistances(capture.Distances);
        }
    }
}
