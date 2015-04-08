using System.Threading;
using Microsoft.SPOT.Hardware;

namespace IngenuityMicro.Molecule.Oxygen.Hardware
{
    /// <summary>
    /// Hardware definitions and helper functions for the Oxygen processor board
    /// </summary>
    public class Hardware
    {
        /// <summary>
        /// Output pin used for controlling the power to the Rf sub-assembly
        /// </summary>
        public static readonly OutputPort RfPower = new OutputPort(Pin.PB3, false);
        /// <summary>
        /// Output pin connected to the on-board user LED
        /// </summary>
        public static readonly OutputPort UserLed = new OutputPort(Pin.PA13, false);

        /// <summary>
        /// Turn power on/off to the RF Header
        /// </summary>
        /// <param name="power">boolean</param>
        public static void EnableRfPower(bool power = true)
        {
            RfPower.Write(power);
        }

        /// <summary>
        /// On board user LED
        /// </summary>
        /// <param name="on">bool</param>
        public static void SetUserLed(bool on = true)
        {
            UserLed.Write(on);
        }
        
        /// <summary>
        /// Blink the onboard LED
        /// </summary>
        /// <param name="num">Number of blinks</param>
        /// <param name="delay">delay in ms</param>
        public static void BlinkUserLed(int num = 2, int delay = 100)
        {
            for (int i = 0; i < num; i++)
            {
                UserLed.Write(true);
                Thread.Sleep(delay);
                UserLed.Write(false);
                Thread.Sleep(delay);
            }
        }
    }
}
