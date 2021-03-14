using AutoMapper;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingTerminal.Models.Bindables;

namespace TradingTerminal.Models
{
    public class ExtendedBindableBase : StorageBindableBase
    {
        protected readonly IEventAggregator _eventAggregator;
        protected readonly IMapper _mapper;

        public ExtendedBindableBase(IEventAggregator eventAggregator, IMapper mapper)
        {
            _eventAggregator = eventAggregator;
            _mapper = mapper;
        }   
    }
}
