﻿using System.Collections.Generic;
using System.IO;
using WolvenKit.CR2W.Editors;

namespace WolvenKit.CR2W.Types
{
    public class CMeshComponent : CVector
    {
        public CArray attachments;

        public CMeshComponent(CR2WFile cr2w) :
            base(cr2w)
        {
            attachments = new CArray("[]handle:attachment", "handle:attachment", true, cr2w);
            attachments.Name = "attachments";
        }

        public override void Read(BinaryReader file, uint size)
        {
            base.Read(file, size);

            attachments.Read(file, size);
        }

        public override void Write(BinaryWriter file)
        {
            base.Write(file);

            attachments.Write(file);
        }

        public override CVariable SetValue(object val)
        {
            return this;
        }

        public override CVariable Create(CR2WFile cr2w)
        {
            return new CMeshComponent(cr2w);
        }

        public override CVariable Copy(CR2WCopyAction context)
        {
            var var = (CMeshComponent) base.Copy(context);
            var.attachments = (CArray) attachments.Copy(context);
            return var;
        }

        public override List<IEditableVariable> GetEditableVariables()
        {
            var list = new List<IEditableVariable>(variables);
            list.Add(attachments);
            return list;
        }
    }
}