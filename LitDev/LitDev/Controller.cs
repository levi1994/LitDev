﻿//The following Copyright applies to the LitDev Extension for Small Basic and files in the namespace LitDev.
//Copyright (C) <2011 - 2015> litdev@hotmail.co.uk
//This file is part of the LitDev Extension for Small Basic.

//LitDev Extension is free software: you can redistribute it and/or modify
//it under the terms of the GNU General Public License as published by
//the Free Software Foundation, either version 3 of the License, or
//(at your option) any later version.

//LitDev Extension is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//GNU General Public License for more details.

//You should have received a copy of the GNU General Public License
//along with menu.  If not, see <http://www.gnu.org/licenses/>.

using System.Collections.Generic;
using Microsoft.SmallBasic.Library;
using SlimDX.DirectInput;
using LitDev.Engines;

namespace LitDev
{
    /// <summary>
    /// Get input from one or more USB attached game controllers (e.g. gamepad or joystick).  If there is more than 1 attached device, then they are numbered from 1.
    /// 
    /// SlimDX runtme for .Net 4.0 requires to be installed before this object can be used (http://slimdx.org/download.php).
    /// </summary>
    [SmallBasicType]
    public static class LDController
    {
        private static DirectInput directInput;
        private static List<Joystick> joysticks = new List<Joystick>();
        private static int scale = 100;

        private static void Clear()
        {
            foreach (Joystick joystick in joysticks)
            {
                joystick.Dispose();
            }
            joysticks.Clear();
        }

        private static int Aquire()
        {
            directInput = new DirectInput();
            Clear();
            foreach (DeviceInstance device in directInput.GetDevices(DeviceClass.GameController, DeviceEnumerationFlags.AttachedOnly))
            {
                Joystick joystick = new Joystick(directInput, device.InstanceGuid);
                joystick.Acquire();
                foreach (DeviceObjectInstance deviceObject in joystick.GetObjects())
                {
                    if ((deviceObject.ObjectType & ObjectDeviceType.Axis) != 0)
                    {
                        joystick.GetObjectPropertiesById((int)deviceObject.ObjectType).SetRange(-scale, scale);
                    }
                }
                joysticks.Add(joystick);
            }
            return joysticks.Count;
        }

        private static Primitive _Buttons(Primitive controller)
        {
            if (controller > joysticks.Count && controller > Aquire()) return "";
            bool[] buttons= joysticks[controller-1].GetCurrentState().GetButtons();
            string result = "";
            for (int i = 0; i < joysticks[controller - 1].Capabilities.ButtonCount; i++)
            {
                result += (i + 1).ToString() + "=" + (buttons[i] ? "True" : "False") + ";";
            }
            return Utilities.CreateArrayMap(result);
        }

        private static Primitive _Sliders(Primitive controller)
        {
            if (controller > joysticks.Count && controller > Aquire()) return "";
            int[] sliders = joysticks[controller-1].GetCurrentState().GetSliders();
            string result = "";
            for (int i = 0; i < sliders.Length; i++)
            {
                result += (i + 1).ToString() + "=" + ((scale + sliders[i])/2.0).ToString() + ";";
            }
            return Utilities.CreateArrayMap(result);
        }

        private static Primitive _POV(Primitive controller)
        {
            if (controller > joysticks.Count && controller > Aquire()) return "";
            int[] pov = joysticks[controller-1].GetCurrentState().GetPointOfViewControllers();
            string result = "";
            for (int i = 0; i < joysticks[controller - 1].Capabilities.PovCount; i++)
            {
                result += (i + 1).ToString() + "=" + (pov[i]/(double)scale).ToString() + ";";
            }
            return Utilities.CreateArrayMap(result);
        }

        private static Primitive _Position(Primitive controller)
        {
            if (controller > joysticks.Count && controller > Aquire()) return "";
            string result = "1=" + joysticks[controller-1].GetCurrentState().X.ToString() + ";";
            result += "2=" + joysticks[controller-1].GetCurrentState().Y.ToString() + ";";
            result += "3=" + joysticks[controller-1].GetCurrentState().Z.ToString() + ";";
            return Utilities.CreateArrayMap(result);
        }

        private static Primitive _Rotation(Primitive controller)
        {
            if (controller > joysticks.Count && controller > Aquire()) return "";
            string result = "1=" + joysticks[controller-1].GetCurrentState().RotationX.ToString() + ";";
            result += "2=" + joysticks[controller-1].GetCurrentState().RotationY.ToString() + ";";
            result += "3=" + joysticks[controller-1].GetCurrentState().RotationZ.ToString() + ";";
            return Utilities.CreateArrayMap(result);
        }

        /// <summary>
        /// Get the number of attached controllers.
        /// </summary>
        public static Primitive Count
        {
            get
            {
                if (!VerifySlimDX.Verify(Utilities.GetCurrentMethod())) return "";
                return Aquire();
            }
        }

        /// <summary>
        /// Get the pressed state of controller buttons.
        /// </summary>
        /// <param name="controller">A USB attached controller number (e.g. joystick or gamepad) indexed from 1.</param>
        /// <returns>An array of button states ("True" or "False")</returns>
        public static Primitive Buttons(Primitive controller)
        {
            if (!VerifySlimDX.Verify(Utilities.GetCurrentMethod())) return "";
            return _Buttons(controller);
        }

        /// <summary>
        /// Get the slider position of controller sliders.
        /// </summary>
        /// <param name="controller">A USB attached controller number (e.g. joystick or gamepad) indexed from 1.</param>
        /// <returns>An array of slider positions (0 to 100)</returns>
        public static Primitive Sliders(Primitive controller)
        {
            if (!VerifySlimDX.Verify(Utilities.GetCurrentMethod())) return "";
            return _Sliders(controller);
        }

        /// <summary>
        /// Get the POV (Point Of View) of controller.
        /// </summary>
        /// <param name="controller">A USB attached controller number (e.g. joystick or gamepad) indexed from 1.</param>
        /// <returns>An array of (X,Y,Z) POV values (degrees)</returns>
        public static Primitive POV(Primitive controller)
        {
            if (!VerifySlimDX.Verify(Utilities.GetCurrentMethod())) return "";
            return _POV(controller);
        }

        /// <summary>
        /// Get the position of a controller joystick.
        /// </summary>
        /// <param name="controller">A USB attached controller number (e.g. joystick or gamepad) indexed from 1.</param>
        /// <returns>An array of (X,Y,Z) position values (-100 to 100)</returns>
        public static Primitive Position(Primitive controller)
        {
            if (!VerifySlimDX.Verify(Utilities.GetCurrentMethod())) return "";
            return _Position(controller);
        }

        /// <summary>
        /// Get the rotation of a controller joystick.
        /// </summary>
        /// <param name="controller">A USB attached controller number (e.g. joystick or gamepad) indexed from 1.</param>
        /// <returns>An array of (X,Y,Z) rotation values (-100 to 100)</returns>
        public static Primitive Rotation(Primitive controller)
        {
            if (!VerifySlimDX.Verify(Utilities.GetCurrentMethod())) return "";
            return _Rotation(controller);
        }
    }
}

