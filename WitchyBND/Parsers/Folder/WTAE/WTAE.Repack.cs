﻿using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using SoulsFormats;
using WitchyFormats;
using WitchyLib;

namespace WitchyBND.Parsers;

public partial class WTAE
{
    public override void Repack(string srcPath)
    {
        if (!WarnAboutTAEs()) return;

        var tae = new TAE();

        XElement xml = LoadXml(GetFolderXmlPath(srcPath));

        var game = Enum.Parse<WBUtil.GameType>(xml.Element("game")!.Value);
        if (!templateDict.ContainsKey(game))
            throw new GameUnsupportedException(game);

        TAE.Template template = templateDict[game];

        DCX.Type compression = Enum.Parse<DCX.Type>(xml.Element("compression")?.Value ?? "None");
        tae.Compression = compression;


        tae.ID = int.Parse(xml.Element("id")!.Value);
        tae.Format = Enum.Parse<TAE.TAEFormat>(xml.Element("format")!.Value);
        tae.EventBank = long.Parse(xml.Element("eventBank")!.Value);
        tae.SibName = xml.Element("sibName")!.Value;
        tae.SkeletonName = xml.Element("skeletonName")!.Value;
        tae.Flags = xml.Element("flags")!.Value.Split(",").Select(s => byte.Parse(s)).ToArray();
        tae.BigEndian = bool.Parse(xml.Element("bigendian")!.Value);

        tae.Animations = new();

        var animFiles = Directory.GetFiles(srcPath, "anim-*.xml").Order();
        foreach (string file in animFiles)
        {
            var id = int.Parse(Path.GetFileNameWithoutExtension(file).Replace("anim-", ""));
            var animXml = XDocument.Load(file).Root!;
            var animName = animXml.Element("name")!.Value;
            var headerEl = animXml.Element("header")!;
            var headerType = Enum.Parse<TAE.Animation.MiniHeaderType>(headerEl.Element("type")!.Value);
            TAE.Animation.AnimMiniHeader header;
            switch (headerType)
            {
                case TAE.Animation.MiniHeaderType.Standard:
                    var standard = new TAE.Animation.AnimMiniHeader.Standard();
                    standard.AllowDelayLoad = bool.Parse(headerEl.Element("allowDelayLoad")!.Value);
                    standard.ImportsHKX = bool.Parse(headerEl.Element("importsHkx")!.Value);
                    standard.IsLoopByDefault = bool.Parse(headerEl.Element("loopByDefault")!.Value);
                    standard.ImportHKXSourceAnimID = int.Parse(headerEl.Element("importHkxSourceAnimId")!.Value);
                    header = standard;
                    break;
                case TAE.Animation.MiniHeaderType.ImportOtherAnim:
                    var import = new TAE.Animation.AnimMiniHeader.ImportOtherAnim();
                    import.ImportFromAnimID = int.Parse(headerEl.Element("animId")!.Value);
                    header = import;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var anim = new TAE.Animation(id, header, animName);
            anim.Events = new();
            anim.EventGroups = new();

            foreach (XElement groupEl in animXml.Element("animGroups")!.Elements("animGroup"))
            {
                var type = long.Parse(groupEl.Element("type")!.Value);
                var group = new TAE.EventGroup(type);

                var data = new TAE.EventGroup.EventGroupDataStruct();
                data.DataType = Enum.Parse<TAE.EventGroup.EventGroupDataType>(groupEl.Element("dataType")!.Value);
                data.Area = sbyte.Parse(groupEl.Element("area")!.Value);
                data.Block = sbyte.Parse(groupEl.Element("block")!.Value);
                data.CutsceneEntityType = Enum.Parse<TAE.EventGroup.EventGroupDataStruct.EntityTypes>(groupEl.Element("cutsceneEntityType")!.Value);
                data.CutsceneEntityIDPart1 = short.Parse(groupEl.Element("cutsceneEntityId1")!.Value);
                data.CutsceneEntityIDPart2 = short.Parse(groupEl.Element("cutsceneEntityId2")!.Value);

                group.GroupData = data;

                anim.EventGroups.Add(group);

                var eventsEl = groupEl.Element("events");
                if (eventsEl != null)
                {
                    foreach (XElement evEl in eventsEl.Elements("event"))
                    {
                        var evType = int.Parse(evEl.Element("type")!.Value);
                        var unk04 = int.Parse(evEl.Element("unk04")!.Value);
                        var startTime = float.Parse(evEl.Element("startTime")!.Value);
                        var endTime = float.Parse(evEl.Element("endTime")!.Value);
                        var isUnk = bool.Parse(evEl.Element("isUnk")?.Value ?? "false");
                        if (!isUnk)
                        {
                            var ev = new TAE.Event(startTime, endTime, evType, unk04, tae.BigEndian, template[tae.EventBank][evType]);
                            ev.Group = group;

                            var paramsEl = groupEl.Element("params");
                            if (paramsEl != null)
                            {
                                foreach (XElement paramEl in paramsEl.Elements("param"))
                                {
                                    var key = paramEl.Attribute("name")!.Value;
                                    var value = paramEl.Attribute("value")!.Value;

                                    ev.Parameters[key] = ev.Parameters.Template[key].StringToValue(value);
                                }
                            }
                            anim.Events.Add(ev);
                        }
                        else
                        {
                            var paramBytes = evEl.Element("unkParams")!.Value.Split(",").Select(s => byte.Parse(s)).ToArray();
                            var ev = new TAE.Event(startTime, endTime, evType, unk04, paramBytes, tae.BigEndian);
                            ev.Group = group;
                            anim.Events.Add(ev);
                        }

                    }
                }
            }
            tae.Animations.Add(anim);
        }
        tae.ApplyTemplate(templateDict[game]);

        string outPath = GetRepackDestPath(srcPath, xml);
        WBUtil.Backup(outPath);
        tae.Write(outPath);
    }
}