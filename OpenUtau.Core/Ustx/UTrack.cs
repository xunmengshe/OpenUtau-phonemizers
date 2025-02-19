﻿using System;
using System.Collections.Generic;
using System.Linq;
using OpenUtau.Api;
using OpenUtau.Core.Render;
using Serilog;
using YamlDotNet.Serialization;

namespace OpenUtau.Core.Ustx {
    public class URenderSettings {
        public string renderer;
        public string resampler;
        public string wavtool;

        [YamlIgnore] public IRenderer Renderer { get; set; }
        [YamlIgnore] public Classic.IResampler Resampler { get; set; }
        [YamlIgnore] public Classic.IWavtool Wavtool { get; set; }

        public void Validate(UTrack track) {
            if (track.Singer == null || !track.Singer.Found) {
                renderer = null;
                Renderer = null;
                resampler = null;
                Resampler = null;
                wavtool = null;
                Wavtool = null;
                return;
            }
            if (string.IsNullOrEmpty(renderer)) {
                renderer = Renderers.GetDefaultRenderer(track.Singer.SingerType);
            }
            if (renderer != Renderer?.ToString()) {
                Renderer = Renderers.CreateRenderer(renderer);
            }
            if (renderer == Renderers.CLASSIC) {
                if (string.IsNullOrEmpty(resampler)) {
                    if (!Util.Preferences.Default.DefaultResamplers.TryGetValue(renderer, out resampler)) {
                        resampler = null;
                    }
                }
                if (string.IsNullOrEmpty(resampler) || resampler != Resampler?.ToString()) {
                    Resampler = Classic.ToolsManager.Inst.GetResampler(resampler);
                    resampler = Resampler.ToString();
                }
                if (string.IsNullOrEmpty(wavtool)) {
                    if (!Util.Preferences.Default.DefaultWavtools.TryGetValue(renderer, out wavtool)) {
                        wavtool = null;
                    }
                }
                if (string.IsNullOrEmpty(wavtool) || wavtool != Wavtool?.ToString()) {
                    Wavtool = Classic.ToolsManager.Inst.GetWavtool(wavtool);
                    wavtool = Wavtool.ToString();
                }
            } else {
                wavtool = null;
                Wavtool = null;
            }
        }

        public URenderSettings Clone() {
            return new URenderSettings {
                renderer = renderer,
                resampler = resampler,
                wavtool = wavtool,
            };
        }
    }

    public class UTrack {
        public string singer;
        public string phonemizer;
        public URenderSettings RendererSettings { get; set; } = new URenderSettings();

        private USinger singer_;

        [YamlIgnore]
        public USinger Singer {
            get => singer_;
            set {
                if (singer_ != value) {
                    singer_ = value;
                    VoiceColorExp = null;
                }
            }
        }
        [YamlIgnore] public Phonemizer Phonemizer { get; set; } = PhonemizerFactory.Get(typeof(DefaultPhonemizer)).Create();
        [YamlIgnore] public string PhonemizerTag => Phonemizer.Tag;

        [YamlIgnore] public string SingerName => Singer != null ? Singer.DisplayName : "[No Singer]";
        [YamlIgnore] public int TrackNo { set; get; }
        public string TrackName { get; set; } = "New Track";
        public bool Mute { set; get; }
        public bool Solo { set; get; }
        public double Volume { set; get; }
        public double Pan { set; get; }
        [YamlIgnore] public UExpressionDescriptor VoiceColorExp { set; get; }

        public UTrack() {
        }
        public UTrack(UProject project) {
            int trackCount = 0;
            if (project.tracks != null && project.tracks.Count > 0) {
                trackCount = project.tracks.Max(t => int.TryParse(t.TrackName.Replace("Track", ""), out int result) ? result : 0);
                if (project.tracks.Count > trackCount) {
                    trackCount = project.tracks.Count;
                }
            }
            TrackName = "Track" + (trackCount + 1);
        }
        public UTrack(string trackName) {
            TrackName = trackName;
        }

        public bool TryGetExpression(UProject project, string key, out UExpressionDescriptor descriptor) {
            if (!project.expressions.TryGetValue(key, out descriptor)) {
                return false;
            }
            if (key == Format.Ustx.CLR && VoiceColorExp != null) {
                descriptor = VoiceColorExp;
            }
            return true;
        }

        public void OnSingerRefreshed() {
            if (Singer != null && Singer.Loaded && !SingerManager.Inst.Singers.ContainsKey(Singer.Id)) {
                Singer = USinger.CreateMissing(Singer.Name);
            }
            VoiceColorExp = null;
        }

        public void Validate(ValidateOptions options, UProject project) {
            if (Singer != null && Singer.Found) {
                Singer.EnsureLoaded();
            }
            if (RendererSettings == null) {
                RendererSettings = new URenderSettings();
            }
            RendererSettings.Validate(this);
            if (project.expressions.TryGetValue(Format.Ustx.CLR, out var descriptor)) {
                if (VoiceColorExp == null && Singer != null && Singer.Found && Singer.Loaded) {
                    VoiceColorExp = descriptor.Clone();
                    var colors = Singer.Subbanks.Select(subbank => subbank.Color).ToHashSet();
                    VoiceColorExp.options = colors.OrderBy(c => c).ToArray();
                    VoiceColorExp.max = VoiceColorExp.options.Length - 1;
                }
            }
        }

        public void BeforeSave() {
            singer = Singer?.Id;
            phonemizer = Phonemizer.GetType().FullName;
        }

        public void AfterLoad(UProject project) {
            if (Phonemizer == null || !string.IsNullOrEmpty(phonemizer)) {
                try {
                    var factory = DocManager.Inst.PhonemizerFactories.FirstOrDefault(factory => factory.type.FullName == phonemizer);
                    Phonemizer = factory?.Create();
                    phonemizer = null;
                } catch (Exception e) {
                    Log.Error(e, $"Failed to load phonemizer {phonemizer}");
                }
            }
            if (Phonemizer == null) {
                Phonemizer = PhonemizerFactory.Get(typeof(DefaultPhonemizer)).Create();
            }
            if (Singer == null && !string.IsNullOrEmpty(singer)) {
                Singer = SingerManager.Inst.GetSinger(singer);
                if (Singer == null) {
                    Singer = USinger.CreateMissing(singer);
                }
            }
            if (RendererSettings == null) {
                RendererSettings = new URenderSettings();
            }
            if (Singer != null && Singer.Found) {
                if (string.IsNullOrEmpty(RendererSettings.renderer)) {
                    RendererSettings.renderer = Renderers.GetDefaultRenderer(Singer.SingerType);
                };
            }
            TrackNo = project.tracks.IndexOf(this);
        }
    }
}
