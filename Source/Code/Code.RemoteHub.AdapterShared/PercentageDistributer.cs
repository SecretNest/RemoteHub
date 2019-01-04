using System;
using System.Collections.Generic;
using System.Text;

namespace SecretNest.RemoteHub
{
    class PercentageDistributer
    {
        KeyValuePair<int, Guid>[] elements;
        int total = 0;
        Random random;
        public PercentageDistributer(Dictionary<Guid, int> values)
        {
            if (values == null || values.Count == 0)
            {
                throw new ArgumentException("Values is null or no element can be found.", nameof(values));
            }
            else if (values.Count == 1)
            {
                elements = new KeyValuePair<int, Guid>[1];

                using (var enumerator = values.Keys.GetEnumerator())
                {
                    enumerator.MoveNext();
                    elements[0] = new KeyValuePair<int, Guid>(0, enumerator.Current);
                }
            }
            else
            {
                random = new Random();
                elements = new KeyValuePair<int, Guid>[values.Count];
                using (var enumerator = values.GetEnumerator())
                {
                    for (int i = 0; i < values.Count; i++)
                    {
                        enumerator.MoveNext();
                        var value = enumerator.Current;
                        if (value.Value <= 0)
                            throw new ArgumentException("Weight must be larger than 0.", nameof(values));
                        total += value.Value;
                        elements[i] = new KeyValuePair<int, Guid>(total, value.Key);
                    }
                }
            }
        }

        public Guid GetOne()
        {
            if (elements.Length == 1)
            {
                return elements[0].Value;
            }
            else
            {
                var value = random.Next(total);
                var lengthMinusOne = elements.Length - 1;

                for (int i = 0; i < lengthMinusOne; i++)
                {
                    var element = elements[i];
                    if (element.Key > value)
                        return element.Value;
                }
                return elements[lengthMinusOne].Value;
            }
        }
    }
}
