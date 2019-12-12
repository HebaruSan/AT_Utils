﻿//   ATMagneticDamper.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2018 Allis Tauri

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AT_Utils
{
    public class ATMagneticDamper : PartModule
    {
        [KSPField(isPersistant = true,
            guiActive = true,
            guiActiveEditor = true,
            guiName = "Attenuation")]
        [UI_FloatEdit(scene = UI_Scene.All,
            minValue = 0f,
            maxValue = 99.9f,
            incrementLarge = 10f,
            incrementSmall = 1f,
            incrementSlide = 0.1f,
            sigFigs = 1,
            unit = "%")]
        public float Attenuation = 50f;

        [KSPField(isPersistant = true,
            guiActive = true,
            guiActiveEditor = true,
            guiName = "Attractor Power")]
        [UI_FloatEdit(scene = UI_Scene.All,
            minValue = 0f,
            incrementLarge = 10f,
            incrementSmall = 1f,
            incrementSlide = 0.1f,
            sigFigs = 1,
            unit = "kN/t")]
        public float AttractorPower = 1f;

        [KSPField] public bool VariableAttractorForce;

        [KSPField] public string DamperID = string.Empty;
        [KSPField] public string Sensor = string.Empty;
        [KSPField] public string AttractorLocation = string.Empty;
        [KSPField] public string AffectedPartTags = string.Empty;
        [KSPField] public bool AffectKerbals;
        [KSPField] public bool EnableControls = true;
        [KSPField] public float MaxForce = 100f;
        [KSPField] public float MaxEnergyConsumption = 50f;
        [KSPField] public float EnergyConsumptionK = 1f;
        [KSPField] public float ReactivateAfterSeconds = 5f;
        private double reactivateAtUT = -1;

        [KSPField] public string AnimatorID = string.Empty;
        private IAnimator animator;

        [KSPField(isPersistant = true)] public bool damperEnabled = false;
        [KSPField(isPersistant = true)] public bool attractorEnabled = true;
        protected Damper damper;
        protected ResourcePump socket;

        public bool HasDamper { get; private set; }
        public bool HasAttractor => HasDamper && damper.HasAttractor;

        public static ATMagneticDamper GetDamper(Part p, string id) =>
            string.IsNullOrEmpty(id)
                ? null
                : p.Modules.GetModules<ATMagneticDamper>()
                    .FirstOrDefault(d =>
                        !string.IsNullOrEmpty(d.DamperID) && d.DamperID.Equals(id));

        public override string GetInfo()
        {
            var info = StringBuilderCache.Acquire();
            info.AppendLine($"Attenuation: {Attenuation:F1} %");
            info.AppendLine($"Max.Force: {MaxForce:F1} kN");
            info.AppendLine($"Max.EC Current: {MaxEnergyConsumption:F1} ec/s");
            if(string.IsNullOrEmpty(AttractorLocation))
                info.AppendLine("Has attractor");
            info.AppendLine(string.IsNullOrEmpty(AffectedPartTags)
                ? "Affects all parts"
                : $"Affects only: {AffectedPartTags}");
            if(AffectKerbals)
                info.AppendLine("WARNING: Affects kerbals!");
            return info.ToStringAndRelease();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            HasDamper = false;
            EnergyConsumptionK = Utils.ClampL(EnergyConsumptionK, 1e-6f);
            if(!string.IsNullOrEmpty(Sensor))
            {
                var sensor = part.FindModelComponent<MeshFilter>(Sensor);
                if(sensor != null)
                {
                    sensor.AddCollider(true);
                    damper = sensor.gameObject.AddComponent<Damper>();
                    damper.Init(this);
                    damper.enabled = damperEnabled;
                    socket = part.CreateSocket();
                    animator = part.GetAnimator(AnimatorID);
                    if(damperEnabled)
                        animator?.Open();
                    else
                        animator?.Close();
                    HasDamper = true;
                }
            }
            var damper_controllable = HasDamper && EnableControls;
            var attractor_controllable = damper_controllable && damper.HasAttractor;
            Utils.EnableField(Fields[nameof(Attenuation)], damper_controllable);
            Events[nameof(ToggleEvent)].active = damper_controllable;
            Actions[nameof(ToggleAction)].active = damper_controllable;
            Utils.EnableField(Fields[nameof(AttractorPower)],
                attractor_controllable && VariableAttractorForce);
            Events[nameof(ToggleAttractorEvent)].active = attractor_controllable;
            Actions[nameof(ToggleAttractorAction)].active = attractor_controllable;
            updatePAW();
        }

        private void OnDestroy()
        {
            if(HasDamper)
                Destroy(damper);
        }

        private void drainEnergy(float rate) =>
            socket.RequestTransfer(rate * TimeWarp.fixedDeltaTime);

        private void FixedUpdate()
        {
            if(FlightDriver.Pause || !HasDamper || socket == null)
                return;
            if(!socket.TransferResource())
                return;
            if(socket.PartialTransfer)
            {
                animator?.Close();
                damper.enabled = false;
                reactivateAtUT = Planetarium.GetUniversalTime() + ReactivateAfterSeconds;
                Utils.Message(ReactivateAfterSeconds,
                    $"[{part.Title()}] Damper deactivated due to lack of EC. Activating in {ReactivateAfterSeconds}");
            }
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            if(!HasDamper)
                return;
            if(damperEnabled
               && !damper.enabled
               && Planetarium.GetUniversalTime() > reactivateAtUT)
            {
                animator?.Open();
                damper.enabled = true;
                Utils.Message($"[{part.Title()}] Damper reactivated"); //debug
            }
        }

        private void updatePAW()
        {
            Events[nameof(ToggleEvent)].guiName = damperEnabled
                ? "Disable Damper"
                : "Enable Damper";
            Events[nameof(ToggleAttractorEvent)].guiName = attractorEnabled
                ? "Disable Attractor"
                : "Enable Attractor";
        }

        public void Enable(bool enable = true)
        {
            if(!HasDamper || enable == damperEnabled)
                return;
            damper.enabled = damperEnabled = enable;
            if(damperEnabled)
                animator?.Open();
            else
                animator?.Close();
            updatePAW();
        }

        public void EnableAttractor(bool enable = true)
        {
            if(!HasDamper || enable == attractorEnabled)
                return;
            attractorEnabled = enable;
            updatePAW();
        }

        [KSPEvent(guiName = "Disable Damper",
            active = true,
            guiActive = true,
            guiActiveEditor = true,
            guiActiveUnfocused = true,
            externalToEVAOnly = false,
            unfocusedRange = 50)]
        public void ToggleEvent(BaseEventDetails data) => Enable(!damperEnabled);

        [KSPAction(guiName = "Toggle Damper")]
        public void ToggleAction(KSPActionParam data) => Enable(!damperEnabled);

        [KSPEvent(guiName = "Disable Attractor",
            active = true,
            guiActive = true,
            guiActiveEditor = true,
            guiActiveUnfocused = true,
            externalToEVAOnly = false,
            unfocusedRange = 50)]
        public void ToggleAttractorEvent(BaseEventDetails data) =>
            EnableAttractor(!attractorEnabled);

        [KSPAction(guiName = "Toggle Attractor")]
        public void ToggleAttractorAction(KSPActionParam data) =>
            EnableAttractor(!attractorEnabled);

        protected class Damper : MonoBehaviour
        {
            private ATMagneticDamper controller;
            private Transform attractor;
            private string[] tags;

            public bool HasAttractor => attractor != null;

            private struct VesselInfo
            {
                public uint id;
                public Vessel vessel;
                public Vector3 position;
                public float energy_consumption;
            }

            private struct RBInfo
            {
                public Rigidbody rb;
                public Vector3 relV;
                public Vector3 dP;
                public Vector3 dAv;
            }

            /// <summary>
            /// For holding damped packed vessels in place. 
            /// </summary>
            private readonly Dictionary<uint, VesselInfo> dampedVessels =
                new Dictionary<uint, VesselInfo>();

            /// <summary>
            /// For damping unpacked vessels, per Rigidbody
            /// </summary>
            private readonly List<RBInfo> dampedBodies =
                new List<RBInfo>();

            public void Init(ATMagneticDamper damper_module)
            {
                controller = damper_module;
                if(!string.IsNullOrEmpty(controller.AttractorLocation))
                    attractor = controller.part.FindModelTransform(controller.AttractorLocation);
                if(!string.IsNullOrEmpty(controller.AffectedPartTags))
                    tags = Utils.ParseLine(controller.AffectedPartTags, Utils.Comma);
                controller.attractorEnabled = attractor != null;
            }

            private void FixedUpdate()
            {
                if(FlightDriver.Pause || controller == null)
                    return;
                if(dampedBodies.Count > 0
                   && controller.part.Rigidbody != null)
                {
                    var A = controller.Attenuation / 100f;
                    var total_energy = 0f;
                    var attractorEnabled = controller.attractorEnabled && attractor != null;
                    var attractorPosition = attractorEnabled ? attractor.position : Vector3.zero;
                    var h = controller.part.Rigidbody;
                    var nBodies = dampedBodies.Count;
                    for(var i = 0; i < nBodies; i++)
                    {
                        var b = dampedBodies[i];
                        if(b.rb == null)
                            continue;
                        var dist = b.rb.position - h.position;
                        b.relV = b.rb.velocity
                                 - h.velocity
                                 - Vector3.Cross(h.angularVelocity, dist);
                        b.dAv = A * (h.angularVelocity - b.rb.angularVelocity);
                        b.dP = A * b.rb.mass * b.relV;
                        if(attractorEnabled)
                        {
                            var d = b.rb.worldCenterOfMass - attractorPosition;
                            var dm = d.magnitude;
                            if(dm > 0)
                            {
                                var rVel2attractor = -Vector3.Dot(b.relV, d) / dm;
                                var dV = Mathf.Min(
                                    controller.part.crashTolerance * 0.9f - rVel2attractor,
                                    TimeWarp.fixedDeltaTime * controller.AttractorPower);
                                if(dV > 0)
                                    b.dP += b.rb.mass * dV * (dm > 1 ? d / dm : d);
                            }
                        }
                        b.dP = b.dP.ClampMagnitudeH(controller.MaxForce * TimeWarp.fixedDeltaTime);
                        var dL = Vector3.Dot(b.dAv.AbsComponents(), b.rb.inertiaTensor);
                        total_energy += b.dP.sqrMagnitude + dL * dL;
                        dampedBodies[i] = b;
                    }
                    if(total_energy > 0)
                    {
                        var energy_consumption = total_energy
                                                 / TimeWarp.fixedDeltaTime
                                                 * controller.EnergyConsumptionK;
                        var K = 1f;
                        if(energy_consumption > controller.MaxEnergyConsumption)
                        {
                            K = Mathf.Sqrt(controller.MaxEnergyConsumption / energy_consumption);
                            energy_consumption = controller.MaxEnergyConsumption;
                        }
                        for(var i = 0; i < nBodies; i++)
                        {
                            var b = dampedBodies[i];
                            if(b.rb == null)
                                continue;
                            if(K < 1)
                            {
                                b.dP *= K;
                                b.dAv *= K;
                            }
                            b.rb.AddTorque(b.dAv, ForceMode.VelocityChange);
                            b.rb.AddForce(-b.dP, ForceMode.Impulse);
                            h.AddForce(b.dP, ForceMode.Impulse);
                        }
                        controller.drainEnergy(energy_consumption);
                    }
                    dampedBodies.Clear();
                }
                if(dampedVessels.Count > 0)
                {
                    var remove_vessels = new List<uint>();
                    foreach(var vsl_info in dampedVessels.Values)
                    {
                        if(vsl_info.vessel != null && vsl_info.vessel.packed)
                        {
                            vsl_info.vessel.SetPosition(
                                transform.TransformPoint(vsl_info.position));
                            if(vsl_info.energy_consumption > 0)
                                controller.drainEnergy(vsl_info.energy_consumption);
                        }
                        else
                            remove_vessels.Add(vsl_info.id);
                    }
                    remove_vessels.ForEach(vsl_id => dampedVessels.Remove(vsl_id));
                }
            }

            private void OnTriggerStay(Collider col)
            {
                if(!enabled
                   || col == null
                   || col.attachedRigidbody == null)
                    return;
                if(!col.CompareTag("Untagged"))
                    return;
                var p = col.attachedRigidbody.GetComponent<Part>();
                if(p == null
                   || p.vessel == null
                   || p.vessel == controller.vessel
                   || !p.vessel.loaded)
                    return;
                if(p.vessel.isEVA && !controller.AffectKerbals)
                    return;
                if(tags != null && !tags.Any(t => p.partInfo.tags.Contains(t)))
                    return;
                if(!p.vessel.packed)
                {
                    var r = col.attachedRigidbody;
                    dampedBodies.Add(new RBInfo { rb = r });
                }
                else
                {
                    if(!dampedVessels.ContainsKey(p.vessel.persistentId))
                    {
                        dampedVessels[p.vessel.persistentId] = new VesselInfo
                        {
                            id = p.vessel.persistentId,
                            vessel = p.vessel,
                            position = transform
                                .InverseTransformPoint(p.vessel.vesselTransform.position),
                            energy_consumption = p.TotalMass()
                                                 * controller.EnergyConsumptionK
                                                 * 0.01f
                        };
                    }
                }
            }
        }
    }
}
