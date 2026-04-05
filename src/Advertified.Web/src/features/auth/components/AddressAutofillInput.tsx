import { type AddressAutofillRetrieveResponse } from '@mapbox/search-js-core';
import { useEffect, useState, type FocusEvent, type InputHTMLAttributes, type PointerEvent } from 'react';

type AddressAutofillComponent = typeof import('@mapbox/search-js-react').AddressAutofill;

export function AddressAutofillInput({
  accessToken,
  onRetrieve,
  inputProps,
}: {
  accessToken: string;
  onRetrieve: (response: AddressAutofillRetrieveResponse) => void;
  inputProps: InputHTMLAttributes<HTMLInputElement>;
}) {
  const [AddressAutofill, setAddressAutofill] = useState<AddressAutofillComponent | null>(null);
  const [shouldLoadAutofill, setShouldLoadAutofill] = useState(false);

  useEffect(() => {
    if (!shouldLoadAutofill || AddressAutofill) {
      return;
    }

    let isMounted = true;
    void import('@mapbox/search-js-react')
      .then((module) => {
        if (isMounted) {
          setAddressAutofill(() => module.AddressAutofill);
        }
      })
      .catch(() => {
        // Keep the plain input available if the autofill library fails to load.
      });

    return () => {
      isMounted = false;
    };
  }, [AddressAutofill, shouldLoadAutofill]);

  const enhancedInputProps = {
    ...inputProps,
    onFocus: (event: FocusEvent<HTMLInputElement>) => {
      setShouldLoadAutofill(true);
      inputProps.onFocus?.(event);
    },
    onPointerEnter: (event: PointerEvent<HTMLInputElement>) => {
      setShouldLoadAutofill(true);
      inputProps.onPointerEnter?.(event);
    },
  };

  if (!AddressAutofill) {
    return <input {...enhancedInputProps} />;
  }

  return (
    <AddressAutofill
      accessToken={accessToken}
      options={{
        country: 'ZA',
        language: 'en',
      }}
      onRetrieve={onRetrieve}
    >
      <input {...enhancedInputProps} />
    </AddressAutofill>
  );
}
