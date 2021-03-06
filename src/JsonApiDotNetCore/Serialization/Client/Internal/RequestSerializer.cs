using System;
using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;
using JsonApiDotNetCore.Serialization.Building;
using JsonApiDotNetCore.Serialization.Objects;
using Newtonsoft.Json;

namespace JsonApiDotNetCore.Serialization.Client.Internal
{
    /// <summary>
    /// Client serializer implementation of <see cref="BaseSerializer"/>.
    /// </summary>
    public class RequestSerializer : BaseSerializer, IRequestSerializer
    {
        private Type _currentTargetedResource;
        private readonly IResourceGraph _resourceGraph;
        private readonly JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings();

        public RequestSerializer(IResourceGraph resourceGraph,
                                IResourceObjectBuilder resourceObjectBuilder)
            : base(resourceObjectBuilder)
        {
            _resourceGraph = resourceGraph ?? throw new ArgumentNullException(nameof(resourceGraph));
        }

        /// <inheritdoc />
        public string Serialize(IIdentifiable resource)
        {
            if (resource == null)
            {
                var empty = Build((IIdentifiable) null, Array.Empty<AttrAttribute>(), Array.Empty<RelationshipAttribute>());
                return SerializeObject(empty, _jsonSerializerSettings);
            }

            _currentTargetedResource = resource.GetType();
            var document = Build(resource, GetAttributesToSerialize(resource), GetRelationshipsToSerialize(resource));
            _currentTargetedResource = null;

            return SerializeObject(document, _jsonSerializerSettings);
        }

        /// <inheritdoc />
        public string Serialize(IReadOnlyCollection<IIdentifiable> resources)
        {
            if (resources == null) throw new ArgumentNullException(nameof(resources));

            IIdentifiable firstResource = resources.FirstOrDefault();

            Document document;
            if (firstResource == null)
            {
                document = Build(resources, Array.Empty<AttrAttribute>(), Array.Empty<RelationshipAttribute>());
            }
            else
            {
                _currentTargetedResource = firstResource.GetType();
                var attributes = GetAttributesToSerialize(firstResource);
                var relationships = GetRelationshipsToSerialize(firstResource);

                document = Build(resources, attributes, relationships);
                _currentTargetedResource = null;
            }

            return SerializeObject(document, _jsonSerializerSettings);
        }

        /// <inheritdoc />
        public IReadOnlyCollection<AttrAttribute> AttributesToSerialize { private get; set; }

        /// <inheritdoc />
        public IReadOnlyCollection<RelationshipAttribute> RelationshipsToSerialize { private get; set; }

        /// <summary>
        /// By default, the client serializer includes all attributes in the result,
        /// unless a list of allowed attributes was supplied using the <see cref="AttributesToSerialize"/>
        /// method. For any related resources, attributes are never exposed.
        /// </summary>
        private IReadOnlyCollection<AttrAttribute> GetAttributesToSerialize(IIdentifiable resource)
        {
            var currentResourceType = resource.GetType();
            if (_currentTargetedResource != currentResourceType)
                // We're dealing with a relationship that is being serialized, for which
                // we never want to include any attributes in the payload.
                return new List<AttrAttribute>();

            if (AttributesToSerialize == null)
                return _resourceGraph.GetAttributes(currentResourceType);

            return AttributesToSerialize;
        }

        /// <summary>
        /// By default, the client serializer does not include any relationships
        /// for resources in the primary data unless explicitly included using
        /// <see cref="RelationshipsToSerialize"/>.
        /// </summary>
        private IReadOnlyCollection<RelationshipAttribute> GetRelationshipsToSerialize(IIdentifiable resource)
        {
            var currentResourceType = resource.GetType();
            // only allow relationship attributes to be serialized if they were set using
            // <see cref="RelationshipsToInclude{T}(Expression{Func{T, dynamic}})"/>
            // and the current resource is a primary entry.
            if (RelationshipsToSerialize == null)
                return _resourceGraph.GetRelationships(currentResourceType);

            return RelationshipsToSerialize;
        }
    }
}
