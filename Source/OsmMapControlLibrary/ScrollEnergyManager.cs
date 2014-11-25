namespace OsmMapControlLibrary
{
    public class ScrollEnergyManager
    {
        private const double MaxEnergy = 2;
        private const double RechargeRate = 0.03;
        private const double RequestRate = 0.2;
        public double CurrentEnergy { get; set; }

        /// <summary>
        ///     Requests the energy. Not the complete request will be returned
        /// </summary>
        /// <param name="requiredEnergy"></param>
        /// <returns></returns>
        public double RequestEnergy(double requiredEnergy)
        {
            if (requiredEnergy < 0)
            {
                return -RequestEnergy(-requiredEnergy);
            }
            double available = CurrentEnergy*RequestRate;
            if (available < requiredEnergy)
            {
                requiredEnergy = available;
            }
            CurrentEnergy -= available;
            return available;
        }

        /// <summary>
        ///     Recharges the energy (done during every frame)
        /// </summary>
        public void Recharge()
        {
            double diff = MaxEnergy - CurrentEnergy;
            diff *= RechargeRate;
            CurrentEnergy += diff;
        }

        public override string ToString()
        {
            return string.Format("{0:n2} of {1}", CurrentEnergy, MaxEnergy);
        }
    }
}