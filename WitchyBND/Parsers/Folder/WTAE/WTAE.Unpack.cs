﻿using System.IO;
using System.Linq;
using System.Xml.Linq;
using SoulsFormats;
using WitchyFormats;
using WitchyLib;

namespace WitchyBND.Parsers;

public partial class WTAE
{
    public override void Unpack(string srcPath, ISoulsFile? file)
    {
        TAE tae = (file as TAE)!;

        var game = WBUtil.DetermineGameType(srcPath, Configuration.Args.Passive, false).Item1;
        if (!templateDict.ContainsKey(game))
        {
            throw new GameUnsupportedException(game);
        }

        var template = templateDict[game];
        tae.ApplyTemplate(template);
        string destDir = GetUnpackDestPath(srcPath);
        Directory.CreateDirectory(destDir);

        XDocument xDoc = new XDocument();
        XElement root = new XElement(XmlTag);
        xDoc.Add(root);
        root.AddE("id", tae.ID);
        root.AddE("compression", tae.Compression);
        root.AddE("game", game);
        root.AddE("format", tae.Format);
        root.AddE("eventBank", tae.EventBank);
        root.AddE("sibName", tae.SibName);
        root.AddE("skeletonName", tae.SkeletonName);
        root.AddE("flags", string.Join(",", tae.Flags));
        root.AddE("bigendian", tae.BigEndian);

        tae.Animations.ForEach(a => UnpackAnim(destDir, tae, a));

        var destPath = GetFolderXmlPath(destDir);
        AddLocationToXml(srcPath, root);
        xDoc.Save(destPath);
    }

    public void UnpackAnim(string destDir, TAE tae, TAE.Animation anim)
    {
        XDocument xDoc = new XDocument();
        XElement root = new XElement("anim");
        xDoc.Add(root);
        root.AddE("name", anim.AnimFileName);
        var header = new XElement("header");
        root.Add(header);
        header.AddE("type", anim.MiniHeader.Type);
        if (anim.MiniHeader is TAE.Animation.AnimMiniHeader.Standard standard)
        {
            header.AddE("allowDelayLoad", standard.AllowDelayLoad);
            header.AddE("importsHkx", standard.ImportsHKX);
            header.AddE("loopByDefault", standard.IsLoopByDefault);
            header.AddE("importHkxSourceAnimId", standard.ImportHKXSourceAnimID);
        }
        else if (anim.MiniHeader is TAE.Animation.AnimMiniHeader.ImportOtherAnim import)
        {
            header.AddE("animId", import.ImportFromAnimID);
        }

        root.AddE("animGroups", anim.EventGroups.Select(group => {
            var groupEl = new XElement("animGroup");
            groupEl.AddE("type", group.GroupType);
            groupEl.AddE("dataType", group.GroupData.DataType);
            groupEl.AddE("area", group.GroupData.Area);
            groupEl.AddE("block", group.GroupData.Block);
            groupEl.AddE("cutsceneEntityType", group.GroupData.CutsceneEntityType);
            groupEl.AddE("cutsceneEntityId1", group.GroupData.CutsceneEntityIDPart1);
            groupEl.AddE("cutsceneEntityId2", group.GroupData.CutsceneEntityIDPart2);
            var events = anim.Events.Where(a => a.Group == group).ToList();
            if (events.Any())
            {
                groupEl.AddE("events", events.Select(ev => {
                    var eventEl = new XElement("event");
                    eventEl.AddE("type", ev.Type);
                    eventEl.AddE("unk04", ev.Unk04);
                    eventEl.AddE("startTime", ev.StartTime);
                    eventEl.AddE("endTime", ev.EndTime);
                    if (tae.AppliedTemplate[tae.EventBank].ContainsKey(ev.Type))
                    {
                        eventEl.AddE("params", ev.Parameters?.Values.Select(p => {
                            var paramEl = new XElement("param");
                            paramEl.SetAttributeValue("name", p.Key);
                            paramEl.SetAttributeValue("value", p.Value);
                            return paramEl;
                        }));
                    }
                    else
                    {
                        eventEl.AddE("isUnk", true);
                        eventEl.AddE("unkParams", string.Join(",", ev.GetParameterBytes(tae.BigEndian)));
                    }
                    return eventEl;
                }));
            }
            return groupEl;
        }));

        xDoc.Save(Path.Combine(destDir, $"anim-{anim.ID:000000}.xml"));
    }
}