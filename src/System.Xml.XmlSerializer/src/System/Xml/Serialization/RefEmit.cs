using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Reflection;
using System.Diagnostics;
using System.Xml.Extensions;
using Stack = System.Collections.Generic.Stack<object>;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.Metadata.Ecma335.Blobs;
using System.Reflection.PortableExecutable;

namespace System.Xml.XmlSerialization
{
    internal class AssemblyBuilder
    {
        private string name;
        private PEBuilder peBuilder;

        private BlobBuilder ilBuilder;
        private BlobBuilder metadataBlobBuilder;
        private BlobBuilder mappedFieldDataBuilder;
        private BlobBuilder managedResourceDataBuilder;

        private MetadataBuilder metadata;
        private MethodDefinitionHandle mainMethodDef;

        private Assembly assembly;

        internal AssemblyBuilder(string name)
        {
            this.name = name;

            peBuilder = new PEBuilder(
            machine: 0,
            sectionAlignment: 0x2000,
            fileAlignment: 0x200,
            imageBase: 0x00400000,
            majorLinkerVersion: 0x30, // (what is ref.emit using?)
            minorLinkerVersion: 0,
            majorOperatingSystemVersion: 4,
            minorOperatingSystemVersion: 0,
            majorImageVersion: 0,
            minorImageVersion: 0,
            majorSubsystemVersion: 4,
            minorSubsystemVersion: 0,
            subsystem: Subsystem.WindowsCui,
            dllCharacteristics: DllCharacteristics.DynamicBase | DllCharacteristics.NxCompatible | DllCharacteristics.NoSeh | DllCharacteristics.TerminalServerAware,
            imageCharacteristics: Characteristics.ExecutableImage,
            sizeOfStackReserve: 0x00100000,
            sizeOfStackCommit: 0x1000,
            sizeOfHeapReserve: 0x00100000,
            sizeOfHeapCommit: 0x1000);

            ilBuilder = new BlobBuilder();
            metadataBlobBuilder = new BlobBuilder();
            mappedFieldDataBuilder = new BlobBuilder();
            managedResourceDataBuilder = new BlobBuilder();

            metadata = new MetadataBuilder();
        }

        internal void WriteContentTo(Stream peStream)
        {
            var peDirectoriesBuilder = new PEDirectoriesBuilder();

            peBuilder.AddManagedSections(
                peDirectoriesBuilder,
                new TypeSystemMetadataSerializer(metadata, "v4.0.30319", isMinimalDelta: false),
                ilBuilder,
                mappedFieldDataBuilder,
                managedResourceDataBuilder,
                nativeResourceSectionSerializer: null,
                strongNameSignatureSize: 0,
                entryPoint: mainMethodDef,
                pdbPathOpt: null,
                nativePdbContentId: default(ContentId),
                portablePdbContentId: default(ContentId),
                corFlags: CorFlags.ILOnly);

            var peBlob = new BlobBuilder();
            ContentId peContentId;
            peBuilder.Serialize(peBlob, peDirectoriesBuilder, out peContentId);
            peBlob.WriteContentTo(peStream);
        }

        internal void SetCustomAttribute(ConstructorInfo securityTransparentAttribute_ctor)
        {
            //throw new NotImplementedException();
        }

        internal Assembly Assembly
        {
            get
            {
                if (assembly == null)
                {
                    using (var stream = new MemoryStream())
                    {
                        WriteContentTo(stream);
                        assembly = new LoadContext().LoadFromStream(stream);
                    }
                }
                return assembly;
            }
        }
    }

    internal class ModuleBuilder
    {

    }

    internal sealed class LoadContext : AssemblyLoadContext
    {
        internal LoadContext() { }
        public new Assembly LoadFromStream(Stream assembly) => base.LoadFromStream(assembly);
        public new Assembly LoadFromStream(Stream assembly, Stream assemblySymbols) => base.LoadFromStream(assembly, assemblySymbols);
        public new Assembly LoadFromAssemblyPath(string assemblyPath) => base.LoadFromAssemblyPath(assemblyPath);

        protected override Assembly Load(AssemblyName assemblyName)
        {
            throw new NotImplementedException();
        }
    }

    internal class Label
    {
        private Dictionary<int, Blob> usage = new Dictionary<int, Blob>();
        private int offset;
        private bool isMarked = false;

        internal Label()
        {
        }

        internal void UseLabel(InstructionEncoder encoder)
        {
            if (!isMarked)
            {
                usage.Add(encoder.Offset, encoder.Builder.ReserveBytes(1));
            }
            else
            {
                WriteEndBlob(encoder.Builder.WriteByte((byte)(offset - encoder.Offset)));
            }
        }

        private void WriteEndBlob(Blob blob, int fromOffset)
        {
            new BlobWriter(blob).WriteByte((byte)(offset - fromOffset));
        }

        internal void MarkLabel(InstructionEncoder encoder)
        {
            Debug.Assert(!isMarked);

            offset = encoder.Offset;
            foreach (var entry in usage)
            {
                WriteEndBlob(entry.Value, entry.Key);
            }
            usage = null;
            isMarked = true;
        }
    }
}
